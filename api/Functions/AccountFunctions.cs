using System.Text.Json;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Models;
using BulletinBoard.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace BulletinBoard.Api.Functions;

/// <summary>
/// Account + GDPR endpoints: current identity, consent capture, data export
/// (portability) and erasure (right to be forgotten).
/// </summary>
public class AccountFunctions
{
    /// <summary>Comfortably under the column's 200, and a sane length for a name.</summary>
    private const int MaxDisplayNameLength = 60;

    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;
    private readonly AuditService _audit;
    private readonly BlobService _blob;

    public AccountFunctions(AppDbContext db, CurrentUserService users, AuditService audit, BlobService blob)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _blob = blob;
    }

    [Function("Me")]
    public async Task<IActionResult> Me(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "account/me")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        var roles = CurrentUserService.EffectiveRoles(me).ToArray();
        return new OkObjectResult(new MeDto(me.Id, me.Email, me.DisplayName, roles, me.ConsentedAt is not null, me.Status.ToString(), me.StatusReason));
    }

    /// <summary>
    /// Change the display name shown next to the member's posts. The email is not
    /// editable: it comes from the identity provider and is how the account is matched.
    /// </summary>
    [Function("UpdateProfile")]
    public async Task<IActionResult> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "account/me")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var body = await req.ReadJsonAsync<UpdateProfileRequest>();
        var name = body?.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new BadRequestObjectResult("A display name is required.");
        if (name.Length > MaxDisplayNameLength)
            return new BadRequestObjectResult($"The display name may be at most {MaxDisplayNameLength} characters.");

        me.DisplayName = name;
        await _db.SaveChangesAsync();

        var roles = CurrentUserService.EffectiveRoles(me).ToArray();
        return new OkObjectResult(new MeDto(me.Id, me.Email, me.DisplayName, roles, me.ConsentedAt is not null, me.Status.ToString(), me.StatusReason));
    }

    [Function("Consent")]
    public async Task<IActionResult> Consent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "account/consent")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        me.ConsentedAt = DateTimeOffset.UtcNow;
        _audit.Record("gdpr.consent", me.Id);
        await _db.SaveChangesAsync();
        return new NoContentResult();
    }

    /// <summary>Right to access / portability: everything we hold about the caller, as JSON.</summary>
    [Function("ExportMyData")]
    public async Task<IActionResult> Export(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "account/export")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var export = new
        {
            profile = new { me.Id, me.Email, me.DisplayName, me.CreatedAt, me.ConsentedAt },
            roles = CurrentUserService.EffectiveRoles(me),
            events = await _db.Events.Where(e => e.AuthorId == me.Id)
                .Select(e => new { e.Id, e.Title, e.Description, e.Status, e.CreatedAt }).ToListAsync(),
            services = await _db.Services.Where(s => s.AuthorId == me.Id)
                .Select(s => new { s.Id, s.Type, s.Title, s.Description, s.CreatedAt }).ToListAsync(),
            goods = await _db.Goods.Where(g => g.AuthorId == me.Id)
                .Select(g => new { g.Id, g.Title, g.Description, g.Price, g.Currency, g.CreatedAt }).ToListAsync(),
            messages = await _db.Messages.Where(m => m.SenderId == me.Id)
                .Select(m => new { m.Id, m.ConversationId, m.Body, m.SentAt }).ToListAsync()
        };

        _audit.Record("gdpr.export", me.Id);
        await _db.SaveChangesAsync();

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        return new FileContentResult(System.Text.Encoding.UTF8.GetBytes(json), "application/json")
        {
            FileDownloadName = "my-bulletinboard-data.json"
        };
    }

    /// <summary>
    /// Right to erasure: anonymise the profile, remove images, tombstone messages and
    /// withdraw listings after clearing their content. The account is marked deleted
    /// so GetRoles stops returning any roles.
    /// </summary>
    [Function("DeleteMyAccount")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "account/delete")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var now = DateTimeOffset.UtcNow;

        var events = await _db.Events.Where(e => e.AuthorId == me.Id).ToListAsync();
        foreach (var e in events)
        {
            if (!string.IsNullOrEmpty(e.ImageBlobName)) await _blob.DeleteAsync(e.ImageBlobName);
            e.Title = string.Empty;
            e.Description = string.Empty;
            e.EventDate = null;
            e.EndDate = null;
            e.Location = null;
            e.ExternalImageUrl = null;
            e.ImageBlobName = null;
            e.RejectionReason = null;
            e.DeletedAt = now;
        }

        var services = await _db.Services.Where(s => s.AuthorId == me.Id).ToListAsync();
        foreach (var s in services)
        {
            s.Title = string.Empty;
            s.Description = string.Empty;
            s.Category = null;
            s.DeletedAt = now;
        }

        var goods = await _db.Goods.Include(g => g.Images).Where(g => g.AuthorId == me.Id).ToListAsync();
        foreach (var g in goods)
        {
            foreach (var img in g.Images) await _blob.DeleteAsync(img.BlobName);
            _db.GoodsImages.RemoveRange(g.Images);
            g.Title = string.Empty;
            g.Description = string.Empty;
            g.Category = null;
            g.Location = null;
            g.Price = 0;
            g.DeletedAt = now;
        }

        var messages = await _db.Messages.Where(m => m.SenderId == me.Id).ToListAsync();
        foreach (var m in messages) { m.Body = string.Empty; m.IsDeleted = true; }

        var contentIds = events.Select(e => e.Id)
            .Concat(services.Select(s => s.Id))
            .Concat(goods.Select(g => g.Id))
            .ToList();
        var auditLogs = await _db.AuditLogs
            .Where(log => log.ActorUserId == me.Id || log.TargetId == me.Id ||
                (log.TargetId != null && contentIds.Contains(log.TargetId.Value)))
            .ToListAsync();
        foreach (var log in auditLogs) log.Detail = null;

        // Remove elevated roles and anonymise the profile.
        var roles = await _db.UserRoles.Where(r => r.UserId == me.Id).ToListAsync();
        _db.UserRoles.RemoveRange(roles);

        me.ProviderUserId = $"deleted:{Guid.NewGuid():N}";
        me.Email = null;
        me.DisplayName = "Deleted user";
        me.ConsentedAt = null;
        me.StatusReason = null;
        me.Status = AccessStatus.Pending;
        me.DecidedAt = null;
        me.DecidedByUserId = null;
        me.DeletedAt = now;

        _audit.Record("gdpr.erase", me.Id, "user", me.Id);
        await _db.SaveChangesAsync();

        return new OkObjectResult(new { deleted = true });
    }
}
