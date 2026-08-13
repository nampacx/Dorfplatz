using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Models;
using BulletinBoard.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace BulletinBoard.Api.Functions;

public class ServicesFunctions
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;

    public ServicesFunctions(AppDbContext db, CurrentUserService users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>Only the author (or an admin) may change a post.</summary>
    private static bool MayEdit(ServicePost s, AppUser? viewer) =>
        viewer is not null && (s.AuthorId == viewer.Id || CurrentUserService.IsAdmin(viewer));

    private static ServiceDto Map(ServicePost s, AppUser? viewer) => new(
        s.Id, s.Type.ToString(), s.Title, s.Description, s.Category, s.Status.ToString(),
        s.Author?.DisplayName ?? "Unknown", s.AuthorId, s.CreatedAt, MayEdit(s, viewer));

    /// <summary>Public list, optionally filtered by type (seeking|offering).</summary>
    [Function("ListServices")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "services")] HttpRequest req)
    {
        // Dienstleistungen and Marktplatz are for members: an approved account is
        // required to read them, unlike the events board.
        var viewer = await _users.GetOrCreateUserAsync(req);
        if (viewer is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(viewer)) return HttpResults.PendingApproval();

        var query = _db.Services.Include(s => s.Author)
            .Where(s => s.DeletedAt == null && s.Status == ServiceStatus.Active);

        var typeParam = req.Query["type"].ToString();
        if (Enum.TryParse<ServiceType>(typeParam, ignoreCase: true, out var type))
            query = query.Where(s => s.Type == type);

        var items = await query.OrderByDescending(s => s.CreatedAt).Take(200).ToListAsync();
        return new OkObjectResult(items.Select(s => Map(s, viewer)));
    }

    [Function("CreateService")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "services")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var body = await req.ReadJsonAsync<CreateServiceRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description)
            || !Enum.TryParse<ServiceType>(body.Type, ignoreCase: true, out var type))
            return new BadRequestObjectResult("Type (seeking|offering), title and description are required.");

        var s = new ServicePost
        {
            Type = type,
            Title = body.Title.Trim(),
            Description = body.Description.Trim(),
            Category = body.Category?.Trim(),
            AuthorId = me.Id
        };
        _db.Services.Add(s);
        await _db.SaveChangesAsync();
        s.Author = me;
        return new CreatedResult($"/api/services/{s.Id}", Map(s, me));
    }

    /// <summary>Edit a post. Author or admin only.</summary>
    [Function("UpdateService")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "services/{id:guid}")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var s = await _db.Services.Include(x => x.Author)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (s is null) return new NotFoundResult();
        if (!MayEdit(s, me)) return HttpResults.Forbidden();

        var body = await req.ReadJsonAsync<UpdateServiceRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description)
            || !Enum.TryParse<ServiceType>(body.Type, ignoreCase: true, out var type))
            return new BadRequestObjectResult("Type (seeking|offering), title and description are required.");

        s.Type = type;
        s.Title = body.Title.Trim();
        s.Description = body.Description.Trim();
        s.Category = body.Category?.Trim();
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return new OkObjectResult(Map(s, me));
    }

    /// <summary>Remove a post. Author or admin only; soft delete keeps the record.</summary>
    [Function("DeleteService")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "services/{id:guid}")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (s is null) return new NotFoundResult();
        if (!MayEdit(s, me)) return HttpResults.Forbidden();

        s.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new NoContentResult();
    }

    [Function("CloseService")]
    public async Task<IActionResult> Close(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "services/{id:guid}/close")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();
        if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (s is null) return new NotFoundResult();
        if (!MayEdit(s, me)) return HttpResults.Forbidden();

        s.Status = ServiceStatus.Closed;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new NoContentResult();
    }
}
