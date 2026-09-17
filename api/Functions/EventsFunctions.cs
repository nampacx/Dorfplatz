using System.Globalization;
using System.Text;
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
    private const int MaxExportDays = 90;
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
    private static readonly TimeZoneInfo BoardTimeZone = ResolveBoardTimeZone();
    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;
    private readonly AuditService _audit;

    public EventsFunctions(AppDbContext db, CurrentUserService users, AuditService audit)
    {
        _db = db;
        _users = users;
        _audit = audit;
    }

    /// <summary>Only the author (or an admin) may change a submission.</summary>
    private static bool MayEdit(EventPost e, AppUser? viewer) =>
        viewer is not null && (e.AuthorId == viewer.Id || CurrentUserService.IsAdmin(viewer));

    private static string? ValidateSchedule(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (end is not null && start is null)
            return "An end date requires a start date.";
        if (start is not null && end is not null && end < start)
            return "The end date must be on or after the start date.";
        return null;
    }

    private static bool MaySeeSubmissionInfo(AppUser? viewer) =>
        viewer is not null
        && (CurrentUserService.IsInRole(viewer, Roles.Moderator) || CurrentUserService.IsAdmin(viewer));

    private static TimeZoneInfo ResolveBoardTimeZone()
    {
        foreach (var id in new[] { "Europe/Berlin", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static DateTimeOffset ToBoardTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, BoardTimeZone);

    private static DateTimeOffset UpcomingCutoff()
    {
        var now = ToBoardTime(DateTimeOffset.UtcNow);
        var startOfToday = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        return startOfToday.ToUniversalTime();
    }

    private static DateTimeOffset GetBoardDayStart(DateTimeOffset value)
    {
        var local = ToBoardTime(value);
        var dayStart = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(dayStart, BoardTimeZone.GetUtcOffset(dayStart));
    }

    private static bool HasVisibleTime(DateTimeOffset value, bool isEnd) =>
        !(value.Hour == 0 && value.Minute == 0 && value.Second == 0 && value.Millisecond == 0)
        && (!isEnd || value.Hour != 23 || value.Minute != 59 || value.Second != 59 || value.Millisecond != 999);

    private static string FormatBoardDate(DateTimeOffset value) =>
        ToBoardTime(value).ToString("dddd, dd. MMMM yyyy", GermanCulture);

    private static string FormatBoardTime(DateTimeOffset value) =>
        ToBoardTime(value).ToString("HH:mm", GermanCulture);

    private static string FormatSchedule(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null) return "Ohne Termin";

        var boardStart = ToBoardTime(start.Value);
        if (end is null)
            return HasVisibleTime(boardStart, isEnd: false)
                ? $"{FormatBoardDate(start.Value)}, {FormatBoardTime(start.Value)} Uhr"
                : FormatBoardDate(start.Value);

        var boardEnd = ToBoardTime(end.Value);
        var sameDay = boardStart.Date == boardEnd.Date;
        var startTime = HasVisibleTime(boardStart, isEnd: false) ? FormatBoardTime(start.Value) : null;
        var endTime = HasVisibleTime(boardEnd, isEnd: true) ? FormatBoardTime(end.Value) : null;

        if (sameDay)
        {
            if (startTime is not null && endTime is not null) return $"{FormatBoardDate(start.Value)}, {startTime}–{endTime} Uhr";
            if (startTime is not null) return $"{FormatBoardDate(start.Value)}, ab {startTime} Uhr";
            if (endTime is not null) return $"{FormatBoardDate(start.Value)}, bis {endTime} Uhr";
            return FormatBoardDate(start.Value);
        }

        var startText = startTime is null
            ? FormatBoardDate(start.Value)
            : $"{FormatBoardDate(start.Value)}, {startTime} Uhr";
        var endText = endTime is null
            ? FormatBoardDate(end.Value)
            : $"{FormatBoardDate(end.Value)}, {endTime} Uhr";
        return $"{startText} – {endText}";
    }

    private static string FormatExportRange(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        var endInclusive = ToBoardTime(endExclusive.AddMilliseconds(-1));
        return $"{FormatBoardDate(startInclusive)} – {endInclusive.ToString("dddd, dd. MMMM yyyy", GermanCulture)}";
    }

    private static bool TryParseExportDays(string? raw, out int days) =>
        int.TryParse(raw, out days) && days >= 0 && days <= MaxExportDays;

    private static (DateTimeOffset StartInclusive, DateTimeOffset EndExclusive) GetExportWindow(int days)
    {
        var start = UpcomingCutoff();
        return (start, start.AddDays(Math.Max(1, days)));
    }

    private static void AddWrappedLine(List<string> lines, string text, int width = 92, string continuationPrefix = "   ")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            lines.Add(string.Empty);
            return;
        }

        var remaining = text.Trim();
        var prefix = string.Empty;
        while (remaining.Length > width)
        {
            var splitAt = remaining.LastIndexOf(' ', width);
            if (splitAt <= 0) splitAt = width;
            lines.Add(prefix + remaining[..splitAt].TrimEnd());
            remaining = remaining[splitAt..].TrimStart();
            prefix = continuationPrefix;
        }

        lines.Add(prefix + remaining);
    }

    private static string ToPdfHex(string text)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Convert.ToHexString(Encoding.GetEncoding(1252).GetBytes(text));
    }

    private static byte[] BuildPdf(IReadOnlyList<string> lines)
    {
        const int linesPerPage = 48;
        var pages = new List<IReadOnlyList<string>>();
        for (var i = 0; i < lines.Count; i += linesPerPage)
            pages.Add(lines.Skip(i).Take(linesPerPage).ToList());
        if (pages.Count == 0) pages.Add(Array.Empty<string>());

        var objects = new Dictionary<int, byte[]>();
        var pageIds = new List<int>();
        var nextId = 4;

        foreach (var pageLines in pages)
        {
            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 11 Tf");
            content.AppendLine("14 TL");
            content.AppendLine("40 800 Td");
            foreach (var line in pageLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    content.AppendLine("T*");
                    continue;
                }

                content.Append('<').Append(ToPdfHex(line)).AppendLine("> Tj");
                content.AppendLine("T*");
            }
            content.AppendLine("ET");

            var contentBytes = Encoding.ASCII.GetBytes(content.ToString());
            var contentId = nextId++;
            objects[contentId] = Encoding.ASCII.GetBytes(
                $"<< /Length {contentBytes.Length} >>\nstream\n{content}endstream");

            var pageId = nextId++;
            pageIds.Add(pageId);
            objects[pageId] = Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>");
        }

        objects[1] = Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>");
        objects[2] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] >>");
        objects[3] = Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new Dictionary<int, long>();
        foreach (var id in objects.Keys.OrderBy(id => id))
        {
            offsets[id] = stream.Position;
            writer.WriteLine($"{id} 0 obj");
            writer.Flush();
            stream.Write(objects[id]);
            writer.WriteLine();
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xrefOffset = stream.Position;
        writer.WriteLine($"xref\n0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        for (var id = 1; id <= objects.Count; id++)
            writer.WriteLine($"{offsets[id]:D10} 00000 n ");

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefOffset);
        writer.WriteLine("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static byte[] BuildEventsPdf(IReadOnlyList<EventPost> events, DateTimeOffset rangeStart, DateTimeOffset rangeEndExclusive)
    {
        var lines = new List<string>
        {
            "Event-Export",
            $"Zeitraum: {FormatExportRange(rangeStart, rangeEndExclusive)}",
            $"Erstellt am: {ToBoardTime(DateTimeOffset.UtcNow).ToString("dddd, dd. MMMM yyyy 'um' HH:mm 'Uhr'", GermanCulture)}",
            string.Empty
        };

        if (events.Count == 0)
        {
            lines.Add("Keine veröffentlichten Events im gewählten Zeitraum.");
            return BuildPdf(lines);
        }

        for (var i = 0; i < events.Count; i++)
        {
            var item = events[i];
            AddWrappedLine(lines, $"{i + 1}. {item.Title}", continuationPrefix: "   ");
            AddWrappedLine(lines, $"   Termin: {FormatSchedule(item.EventDate, item.EndDate)}");
            AddWrappedLine(lines, $"   Kategorie: {item.Category}");
            if (!string.IsNullOrWhiteSpace(item.Location))
                AddWrappedLine(lines, $"   Ort: {item.Location}");
            if (!string.IsNullOrWhiteSpace(item.Author?.DisplayName))
                AddWrappedLine(lines, $"   Eingereicht von: {item.Author.DisplayName}");

            AddWrappedLine(lines, $"   Beschreibung: {item.Description}", continuationPrefix: "   ");
            lines.Add(string.Empty);
        }

        return BuildPdf(lines);
    }

    private static EventDto Map(EventPost e, AppUser? viewer = null) => new(
        e.Id, e.Title, e.Description, e.EventDate, e.EndDate, e.Location, e.Category.ToString(),
        e.Status.ToString(), e.RejectionReason,
        MaySeeSubmissionInfo(viewer) ? e.Author?.DisplayName ?? "Unknown" : null,
        MaySeeSubmissionInfo(viewer) ? e.CreatedAt : null,
        MayEdit(e, viewer));

    /// <summary>
    /// Public: published events only, and by default only those whose Termin has not
    /// passed yet. Moderators may opt into the archive with ?includePast=true.
    /// Events without a Termin have nothing to expire, so they always stay visible.
    /// </summary>
    [Function("ListEvents")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest req)
    {
        var viewer = await _users.GetOrCreateUserAsync(req);

        var query = _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null && e.Status == EventStatus.Published);

        var includePast = bool.TryParse(req.Query["includePast"].ToString(), out var flag) && flag
            && viewer is not null && CurrentUserService.IsModerator(viewer);
        if (!includePast)
        {
            // Date-only entries are serialized from the browser's local midnight,
            // so the board must compare against its own local day boundary rather
            // than UTC midnight or same-day events disappear too early.
            var cutoff = UpcomingCutoff();
            query = query.Where(e => (e.EndDate ?? e.EventDate) == null || (e.EndDate ?? e.EventDate) >= cutoff);
        }

        if (Enum.TryParse<EventCategory>(req.Query["category"].ToString(), ignoreCase: true, out var category))
            query = query.Where(e => e.Category == category);

        var items = await query
            .OrderByDescending(e => e.EventDate ?? e.CreatedAt)
            .Take(200)
            .ToListAsync();
        return new OkObjectResult(items.Select(e => Map(e, viewer)));
    }

    /// <summary>The caller's own events (any status), so they can see moderation outcomes.</summary>
    [Function("ListMyEvents")]
    public async Task<IActionResult> ListMine(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/mine")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var items = await _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null && e.AuthorId == me.Id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return new OkObjectResult(items.Select(e => Map(e, me)));
    }

    /// <summary>Any authenticated user may submit; the event starts as Pending.</summary>
    [Function("CreateEvent")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var body = await req.ReadJsonAsync<CreateEventRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description))
            return new BadRequestObjectResult("Title and description are required.");

        // Unknown or missing categories fall back to Sonstiges. TryParse alone would
        // leave the enum's first member (Verwaltung), which would be plainly wrong.
        var category = Enum.TryParse<EventCategory>(body.Category, ignoreCase: true, out var parsed)
            ? parsed
            : EventCategory.Sonstiges;
        var scheduleError = ValidateSchedule(body.EventDate, body.EndDate);
        if (scheduleError is not null) return new BadRequestObjectResult(scheduleError);

        var e = new EventPost
        {
            Title = body.Title.Trim(),
            Description = body.Description.Trim(),
            EventDate = body.EventDate,
            EndDate = body.EndDate,
            Location = body.Location?.Trim(),
            Category = category,
            AuthorId = me.Id,
            Status = EventStatus.Pending
        };
        _db.Events.Add(e);
        await _db.SaveChangesAsync();

        e.Author = me;
        return new CreatedResult($"/api/events/{e.Id}", Map(e, me));
    }

    /// <summary>
    /// Edit a submission and resubmit it. Author or admin only.
    /// Any edit returns the event to the moderation queue: a rejected event gets its
    /// second chance, and an already-published one cannot be quietly rewritten into
    /// something that was never approved.
    /// </summary>
    [Function("UpdateEvent")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "events/{id:guid}")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var e = await _db.Events.Include(x => x.Author)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (e is null) return new NotFoundResult();
        if (!MayEdit(e, me)) return HttpResults.Forbidden();

        var body = await req.ReadJsonAsync<UpdateEventRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description))
            return new BadRequestObjectResult("Title and description are required.");

        var category = Enum.TryParse<EventCategory>(body.Category, ignoreCase: true, out var parsed)
            ? parsed
            : EventCategory.Sonstiges;
        var scheduleError = ValidateSchedule(body.EventDate, body.EndDate);
        if (scheduleError is not null) return new BadRequestObjectResult(scheduleError);

        e.Title = body.Title.Trim();
        e.Description = body.Description.Trim();
        e.EventDate = body.EventDate;
        e.EndDate = body.EndDate;
        e.Location = body.Location?.Trim();
        e.Category = category;
        e.UpdatedAt = DateTimeOffset.UtcNow;

        var previousStatus = e.Status;
        e.Status = EventStatus.Pending;
        e.RejectionReason = null;
        e.ModeratedById = null;
        e.ModeratedAt = null;

        _audit.Record("event.resubmit", me.Id, "event", e.Id, new { from = previousStatus.ToString() });
        await _db.SaveChangesAsync();

        return new OkObjectResult(Map(e, me));
    }

    /// <summary>Withdraw a submission. Author or admin only; soft delete.</summary>
    [Function("DeleteEvent")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{id:guid}")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var e = await _db.Events.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (e is null) return new NotFoundResult();
        if (!MayEdit(e, me)) return HttpResults.Forbidden();

        e.DeletedAt = DateTimeOffset.UtcNow;
        _audit.Record("event.delete", me.Id, "event", e.Id);
        await _db.SaveChangesAsync();
        return new NoContentResult();
    }

    /// <summary>Moderator+: the pending queue.</summary>
    [Function("ListPendingEvents")]
    public async Task<IActionResult> ListPending(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "moderation/events")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsModerator(me)) return HttpResults.Forbidden();

        var items = await _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null && e.Status == EventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
        return new OkObjectResult(items.Select(e => Map(e, me)));
    }

    [Function("ExportEventsPdf")]
    public async Task<IActionResult> ExportPdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "moderation/events/export")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsModerator(me)) return HttpResults.Forbidden();
        if (!TryParseExportDays(req.Query["days"], out var days))
            return new BadRequestObjectResult($"The export range must be an integer between 0 and {MaxExportDays}.");

        var (rangeStart, rangeEndExclusive) = GetExportWindow(days);
        var items = await _db.Events
            .Include(e => e.Author)
            .Where(e => e.DeletedAt == null
                && e.Status == EventStatus.Published
                && e.EventDate != null
                && (e.EndDate ?? e.EventDate) >= rangeStart
                && e.EventDate < rangeEndExclusive)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.Title)
            .ToListAsync();

        _audit.Record("event.export", me.Id, "event", detail: new { days, count = items.Count });
        await _db.SaveChangesAsync();

        return new FileContentResult(BuildEventsPdf(items, rangeStart, rangeEndExclusive), "application/pdf")
        {
            FileDownloadName = $"events-{ToBoardTime(rangeStart):yyyyMMdd}-{days}d.pdf"
        };
    }

    [Function("ApproveEvent")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "moderation/events/{id:guid}/approve")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsModerator(me)) return HttpResults.Forbidden();

        var e = await _db.Events.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (e is null) return new NotFoundResult();

        e.Status = EventStatus.Published;
        e.RejectionReason = null;
        e.ModeratedById = me.Id;
        e.ModeratedAt = DateTimeOffset.UtcNow;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Record("event.approve", me.Id, "event", e.Id);
        await _db.SaveChangesAsync();
        return new OkObjectResult(Map(e, me));
    }

    [Function("RejectEvent")]
    public async Task<IActionResult> Reject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "moderation/events/{id:guid}/reject")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
        if (!CurrentUserService.IsModerator(me)) return HttpResults.Forbidden();

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
        return new OkObjectResult(Map(e, me));
    }
}
