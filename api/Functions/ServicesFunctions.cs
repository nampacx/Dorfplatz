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

public class ServicesFunctions
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;

    public ServicesFunctions(AppDbContext db, CurrentUserService users)
    {
        _db = db;
        _users = users;
    }

    private static ServiceDto Map(ServicePost s) => new(
        s.Id, s.Type.ToString(), s.Title, s.Description, s.Category, s.Status.ToString(),
        s.Author?.DisplayName ?? "Unknown", s.AuthorId, s.CreatedAt);

    /// <summary>Public list, optionally filtered by type (seeking|offering).</summary>
    [Function("ListServices")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "services")] HttpRequest req)
    {
        var query = _db.Services.Include(s => s.Author)
            .Where(s => s.DeletedAt == null && s.Status == ServiceStatus.Active);

        var typeParam = req.Query["type"].ToString();
        if (Enum.TryParse<ServiceType>(typeParam, ignoreCase: true, out var type))
            query = query.Where(s => s.Type == type);

        var items = await query.OrderByDescending(s => s.CreatedAt).Take(200).ToListAsync();
        return new OkObjectResult(items.Select(Map));
    }

    [Function("CreateService")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "services")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

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
        return new CreatedResult($"/api/services/{s.Id}", Map(s));
    }

    [Function("CloseService")]
    public async Task<IActionResult> Close(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "services/{id:guid}/close")] HttpRequest req,
        Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var s = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (s is null) return new NotFoundResult();
        if (s.AuthorId != me.Id && !CurrentUserService.IsAdmin(me)) return new ForbidResult();

        s.Status = ServiceStatus.Closed;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new NoContentResult();
    }
}
