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
        if (!CurrentUserService.IsAdmin(me)) return HttpResults.Forbidden();

        var users = await _db.Users.Include(u => u.Roles)
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.Email)
            .ToListAsync();

        var dtos = users.Select(u => new AdminUserDto(
            u.Id, u.Email, u.DisplayName,
            CurrentUserService.EffectiveRoles(u).ToArray(), u.CreatedAt));
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
        return new OkObjectResult(new AdminUserDto(target.Id, target.Email, target.DisplayName, roles, target.CreatedAt));
    }
}
