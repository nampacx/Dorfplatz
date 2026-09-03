using System.Text.Json;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Models;
using BulletinBoard.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace BulletinBoard.Api.Functions;

/// <summary>
/// Admin-only: view members and grant/revoke the moderator role.
///
/// Routes deliberately say "members", not "admin". The Functions host reserves the
/// "admin" path segment for its own management API, so a route of "admin/users" is
/// accepted at indexing time but never dispatched — the caller gets a bare 404 and
/// nothing is logged. See https://github.com/Azure/azure-functions-host/issues/1781.
/// </summary>
public class AdminFunctions
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;
    private readonly AuditService _audit;

    public AdminFunctions(AppDbContext db, CurrentUserService users, AuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    [Function("ListUsers")]
    public async Task<IActionResult> ListUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "members")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsAdmin(me)) return HttpResults.Forbidden();

        var users = await _db.Users.Include(u => u.Roles)
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.Email)
            .ToListAsync();

        var dtos = users.Select(u => new AdminUserDto(
            u.Id, u.Email, u.DisplayName,
            CurrentUserService.EffectiveRoles(u).ToArray(),
            u.Status.ToString(), u.CreatedAt, u.DecidedAt, u.StatusReason));
        return new OkObjectResult(dtos);
    }

    /// <summary>Grant or revoke a role (moderator/admin). Admin only; audit-logged.</summary>
    [Function("UpdateUserRoles")]
    public async Task<IActionResult> UpdateRoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "members/{id:guid}/roles")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsAdmin(me)) return HttpResults.Forbidden();

        var body = await req.ReadJsonAsync<UpdateRolesRequest>();
        if (body is null || (body.Role != Roles.Moderator && body.Role != Roles.Admin))
            return new BadRequestObjectResult("Role must be 'moderator' or 'admin'.");

        // No Include(u => u.Roles): nothing reads the collection before it is reloaded
        // below, and the role we care about is fetched on its own line.
        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);
        if (target is null) return new NotFoundResult();

        var existing = await _db.UserRoles
            .FirstOrDefaultAsync(r => r.UserId == target.Id && r.Role == body.Role);

        if (body.Grant && existing is null)
        {
            // Added through the DbSet rather than target.Roles. UserRole carries a
            // client-side Guid in Id, and an entity reached through a tracked
            // principal's navigation with its key already populated reads to EF as an
            // existing row: it emits UPDATE ... WHERE Id = <new guid>, which matches
            // nothing and throws DbUpdateConcurrencyException. DbSet.Add is unambiguous.
            _db.UserRoles.Add(new UserRole
            {
                Role = body.Role,
                UserId = target.Id,
                GrantedByUserId = me.Id
            });
            _audit.Record("role.grant", me.Id, "user", target.Id, new { role = body.Role });
        }
        else if (!body.Grant && existing is not null)
        {
            // Never let an admin strip their own last admin role by accident.
            if (body.Role == Roles.Admin && target.Id == me.Id)
                return new BadRequestObjectResult("You cannot revoke your own admin role.");
            _db.UserRoles.Remove(existing);
            _audit.Record("role.revoke", me.Id, "user", target.Id, new { role = body.Role });
        }

        await _db.SaveChangesAsync();

        // Re-read rather than trusting the collection loaded before the change.
        await _db.Entry(target).Collection(t => t.Roles).LoadAsync();
        var roles = CurrentUserService.EffectiveRoles(target).ToArray();
        return new OkObjectResult(new AdminUserDto(target.Id, target.Email, target.DisplayName, roles,
            target.Status.ToString(), target.CreatedAt, target.DecidedAt, target.StatusReason));
    }

    /// <summary>
    /// Let a member onto the board, turn them away, or take access away again.
    /// Admin only; audit-logged.
    ///
    /// Rejecting is not a punishment, it is how the queue gets emptied: a
    /// rejected account can still sign in and see that it was not approved,
    /// and an admin can approve it later if that was a mistake.
    ///
    /// Suspending is the opposite direction — it takes access away from someone
    /// who had it. Because that is a decision someone may have to answer for
    /// later, it requires a reason, and so does lifting it again. Both reasons
    /// land in the audit log; the latest one is kept on the user so the member
    /// can be told why.
    /// </summary>
    [Function("UpdateUserApproval")]
    public async Task<IActionResult> UpdateApproval(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "members/{id:guid}/approval")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsAdmin(me)) return HttpResults.Forbidden();

        var body = await req.ReadJsonAsync<UpdateApprovalRequest>();
        if (body is null) return new BadRequestObjectResult("A decision is required.");

        var decision = body.Decision?.Trim().ToLowerInvariant();
        var status = decision switch
        {
            AccessDecisions.Approve => AccessStatus.Approved,
            AccessDecisions.Reject => AccessStatus.Rejected,
            AccessDecisions.Suspend => AccessStatus.Suspended,
            _ => (AccessStatus?)null
        };
        if (status is null)
            return new BadRequestObjectResult("Decision must be 'approve', 'reject' or 'suspend'.");

        var target = await _db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);
        if (target is null) return new NotFoundResult();

        // An admin who revoked their own access would lock themselves out of
        // the page that could undo it.
        if (target.Id == me.Id && status != AccessStatus.Approved)
            return new BadRequestObjectResult("You cannot withdraw your own access.");

        // A reason is demanded for taking access away, and for giving it back
        // after it was taken away — those are the two moves someone might later
        // have to justify. Approving a newcomer or clearing the queue does not
        // need one, and asking for it there would only teach admins to type "ok".
        var reason = body.Reason?.Trim();
        var isReinstatement = status == AccessStatus.Approved && target.Status == AccessStatus.Suspended;
        var needsReason = status == AccessStatus.Suspended || isReinstatement;

        if (needsReason && string.IsNullOrWhiteSpace(reason))
            return new BadRequestObjectResult("A reason is required for this decision.");
        if (reason is { Length: > 500 })
            return new BadRequestObjectResult("The reason must be at most 500 characters.");

        // Suspending someone who was never approved says something that is not
        // true — there is no access to take away. Reject is the move there.
        if (status == AccessStatus.Suspended && target.Status != AccessStatus.Approved)
            return new BadRequestObjectResult("Only an approved member can be suspended.");

        var previous = target.Status;
        target.Status = status.Value;
        target.DecidedAt = DateTimeOffset.UtcNow;
        target.DecidedByUserId = me.Id;
        // Cleared on decisions that carry no reason, so a stale suspension note
        // cannot outlive the suspension and be shown to a readmitted member.
        target.StatusReason = needsReason ? reason : null;

        var action = status switch
        {
            AccessStatus.Suspended => "member.suspend",
            AccessStatus.Rejected => "member.reject",
            _ => isReinstatement ? "member.reinstate" : "member.approve"
        };
        _audit.Record(action, me.Id, "user", target.Id,
            new { from = previous.ToString(), to = target.Status.ToString(), reason });

        await _db.SaveChangesAsync();

        return new OkObjectResult(new AdminUserDto(target.Id, target.Email, target.DisplayName,
            CurrentUserService.EffectiveRoles(target).ToArray(),
            target.Status.ToString(), target.CreatedAt, target.DecidedAt, target.StatusReason));
    }
}
