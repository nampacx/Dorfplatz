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

/// <summary>Admin-only: view members and grant/revoke the moderator role.</summary>
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/users")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsAdmin(me)) return new ForbidResult();

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/users/{id:guid}/roles")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsAdmin(me)) return new ForbidResult();

        var body = await JsonSerializer.DeserializeAsync<UpdateRolesRequest>(req.Body);
        if (body is null || (body.Role != Roles.Moderator && body.Role != Roles.Admin))
            return new BadRequestObjectResult("Role must be 'moderator' or 'admin'.");

        var target = await _db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);
        if (target is null) return new NotFoundResult();

        var existing = target.Roles.FirstOrDefault(r => r.Role == body.Role);
        if (body.Grant && existing is null)
        {
            target.Roles.Add(new UserRole { Role = body.Role, UserId = target.Id, GrantedByUserId = me.Id });
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
        var roles = CurrentUserService.EffectiveRoles(target).ToArray();
        return new OkObjectResult(new AdminUserDto(target.Id, target.Email, target.DisplayName, roles, target.CreatedAt));
    }
}
