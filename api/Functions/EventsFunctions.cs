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

public class EventsFunctions
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;
    private readonly AuditService _audit;

    public EventsFunctions(AppDbContext db, CurrentUserService users, AuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    private static EventDto Map(EventPost e) => new(
        e.Id, e.Title, e.Description, e.EventDate, e.Location, e.Category.ToString(),
        e.Status.ToString(), e.RejectionReason, e.Author?.DisplayName ?? "Unknown", e.CreatedAt);

    /// <summary>Public: published events only.</summary>
    [Function("ListEvents")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest req)
    {
        var query = _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null && e.Status == EventStatus.Published);

        if (Enum.TryParse<EventCategory>(req.Query["category"].ToString(), ignoreCase: true, out var category))
            query = query.Where(e => e.Category == category);

        var items = await query
            .OrderByDescending(e => e.EventDate ?? e.CreatedAt)
            .Take(200)
            .ToListAsync();
        return new OkObjectResult(items.Select(Map));
    }

    /// <summary>The caller's own events (any status), so they can see moderation outcomes.</summary>
    [Function("ListMyEvents")]
    public async Task<IActionResult> ListMine(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/mine")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var items = await _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null && e.AuthorId == me.Id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return new OkObjectResult(items.Select(Map));
    }

    /// <summary>Any authenticated user may submit; the event starts as Pending.</summary>
    [Function("CreateEvent")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var body = await req.ReadJsonAsync<CreateEventRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description))
            return new BadRequestObjectResult("Title and description are required.");

        // Unknown or missing categories fall back to Sonstiges. TryParse alone would
        // leave the enum's first member (Verwaltung), which would be plainly wrong.
        var category = Enum.TryParse<EventCategory>(body.Category, ignoreCase: true, out var parsed)
            ? parsed
            : EventCategory.Sonstiges;

        var e = new EventPost
        {
            Title = body.Title.Trim(),
            Description = body.Description.Trim(),
            EventDate = body.EventDate,
            Location = body.Location?.Trim(),
            Category = category,
            AuthorId = me.Id,
            Status = EventStatus.Pending
        };
        _db.Events.Add(e);
        await _db.SaveChangesAsync();

        e.Author = me;
        return new CreatedResult($"/api/events/{e.Id}", Map(e));
    }

    /// <summary>Moderator+: the pending queue.</summary>
    [Function("ListPendingEvents")]
    public async Task<IActionResult> ListPending(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "moderation/events")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsModerator(me)) return new ForbidResult();

        var items = await _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null && e.Status == EventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
        return new OkObjectResult(items.Select(Map));
    }

    [Function("ApproveEvent")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "moderation/events/{id:guid}/approve")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsModerator(me)) return new ForbidResult();

        var e = await _db.Events.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (e is null) return new NotFoundResult();

        e.Status = EventStatus.Published;
        e.RejectionReason = null;
        e.ModeratedById = me.Id;
        e.ModeratedAt = DateTimeOffset.UtcNow;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Record("event.approve", me.Id, "event", e.Id);
        await _db.SaveChangesAsync();
        return new OkObjectResult(Map(e));
    }

    [Function("RejectEvent")]
    public async Task<IActionResult> Reject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "moderation/events/{id:guid}/reject")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsModerator(me)) return new ForbidResult();

        var body = await req.ReadJsonAsync<RejectEventRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Reason))
            return new BadRequestObjectResult("A rejection reason is required.");

        var e = await _db.Events.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (e is null) return new NotFoundResult();

        e.Status = EventStatus.Rejected;
        e.RejectionReason = body.Reason.Trim();
        e.ModeratedById = me.Id;
        e.ModeratedAt = DateTimeOffset.UtcNow;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Record("event.reject", me.Id, "event", e.Id, new { reason = e.RejectionReason });
        await _db.SaveChangesAsync();
        return new OkObjectResult(Map(e));
    }
}
