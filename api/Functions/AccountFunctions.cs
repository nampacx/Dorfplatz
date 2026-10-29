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
        if (name.Contains('@'))
            return new BadRequestObjectResult("The display name must not contain an email address. You may use a pseudonym.");

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
            profile = new
            {
                me.Id, me.ProviderUserId, me.Email, me.DisplayName, me.CreatedAt, me.ConsentedAt,
                me.DeletedAt, me.Status, me.StatusReason, me.DecidedAt, me.DecidedByUserId,
                Roles = me.Roles.Select(r => new { r.Id, r.UserId, r.Role, r.GrantedAt, r.GrantedByUserId })
            },
            roles = CurrentUserService.EffectiveRoles(me),
            events = await _db.Events.Where(e => e.AuthorId == me.Id)
                .Select(e => new
                {
                    e.Id, e.Title, e.Description, e.EventDate, e.EndDate, e.Location,
                    e.ExternalImageUrl, e.ImageBlobName, e.Category, e.AuthorId, e.Status,
                    e.RejectionReason, e.ModeratedById, e.ModeratedAt, e.CreatedAt, e.UpdatedAt, e.DeletedAt
                }).ToListAsync(),
            services = await _db.Services.Where(s => s.AuthorId == me.Id)
                .Select(s => new
                {
                    s.Id, s.Type, s.Title, s.Description, s.Category, s.AuthorId,
                    s.Status, s.CreatedAt, s.UpdatedAt, s.DeletedAt
                }).ToListAsync(),
            goods = await _db.Goods.Where(g => g.AuthorId == me.Id)
                .Select(g => new
                {
                    g.Id, g.Title, g.Description, g.Price, g.Currency, g.Condition, g.Category,
                    g.Location, g.AuthorId, g.Status, g.CreatedAt, g.UpdatedAt, g.DeletedAt,
                    Images = g.Images.OrderBy(i => i.SortOrder)
                        .Select(i => new { i.Id, i.GoodsPostId, i.BlobName, i.SortOrder }).ToList()
                }).ToListAsync(),
            conversations = await _db.Conversations
                .Where(c => c.ParticipantAId == me.Id || c.ParticipantBId == me.Id)
                .Select(c => new
                {
                    c.Id, c.ListingType, c.ListingId, c.ParticipantAId, c.ParticipantBId,
                    c.CreatedAt, c.LastMessageAt,
                    OtherUserId = c.ParticipantAId == me.Id ? c.ParticipantBId : c.ParticipantAId,
                    OtherDisplayName = c.ParticipantAId == me.Id
                        ? c.ParticipantB.DisplayName : c.ParticipantA.DisplayName
                }).ToListAsync(),
            messages = await _db.Messages
                .Where(m => m.Conversation.ParticipantAId == me.Id || m.Conversation.ParticipantBId == me.Id)
                .Select(m => new
                {
                    m.Id, m.ConversationId, m.SenderId, m.Body, m.SentAt, m.ReadAt, m.IsDeleted
                }).ToListAsync(),
            auditLogs = await _db.AuditLogs
                .Where(a => a.ActorUserId == me.Id ||
                    (a.TargetType == "user" && a.TargetId == me.Id) ||
                    (a.TargetType == "event" && _db.Events.Any(e => e.Id == a.TargetId && e.AuthorId == me.Id)))
                .Select(a => new
                {
                    a.Id, a.Action, a.ActorUserId, a.TargetType, a.TargetId, a.Detail, a.CreatedAt
                }).ToListAsync()
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
    /// withdraw listings. Content authored by the user is soft-deleted; the account is
    /// marked deleted so GetRoles stops returning any roles.
    /// </summary>
    [Function("DeleteMyAccount")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "account/delete")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var now = DateTimeOffset.UtcNow;

        var events = await _db.Events.Where(e => e.AuthorId == me.Id).ToListAsync();
        foreach (var e in events) e.DeletedAt = now;

        var services = await _db.Services.Where(s => s.AuthorId == me.Id).ToListAsync();
        foreach (var s in services) s.DeletedAt = now;

        var goods = await _db.Goods.Include(g => g.Images).Where(g => g.AuthorId == me.Id).ToListAsync();
        foreach (var g in goods)
        {
            foreach (var img in g.Images) await _blob.DeleteAsync(img.BlobName);
            g.DeletedAt = now;
        }

        var messages = await _db.Messages.Where(m => m.SenderId == me.Id).ToListAsync();
        foreach (var m in messages) { m.Body = string.Empty; m.IsDeleted = true; }

        // Remove elevated roles and anonymise the profile.
        var roles = await _db.UserRoles.Where(r => r.UserId == me.Id).ToListAsync();
        _db.UserRoles.RemoveRange(roles);

        me.Email = null;
        me.DisplayName = "Deleted user";
        me.DeletedAt = now;

        _audit.Record("gdpr.erase", me.Id, "user", me.Id);
        await _db.SaveChangesAsync();

        return new OkObjectResult(new { deleted = true });
    }
}
