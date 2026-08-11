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

public class GoodsFunctions
{
    private static readonly TimeSpan UploadTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReadTtl = TimeSpan.FromHours(1);

    private readonly AppDbContext _db;
    private readonly CurrentUserService _users;
    private readonly BlobService _blob;

    public GoodsFunctions(AppDbContext db, CurrentUserService users, BlobService blob)
    {
        _db = db;
        _users = users;
        _blob = blob;
    }

    private async Task<GoodsDto> MapAsync(GoodsPost g)
    {
        var urls = new List<string>();
        foreach (var img in g.Images.OrderBy(i => i.SortOrder))
            urls.Add(await _blob.CreateReadUrlAsync(img.BlobName, ReadTtl));

        return new GoodsDto(g.Id, g.Title, g.Description, g.Price, g.Currency, g.Condition.ToString(),
            g.Category, g.Location, g.Status.ToString(), g.Author?.DisplayName ?? "Unknown", g.AuthorId,
            urls, g.CreatedAt);
    }

    [Function("ListGoods")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "goods")] HttpRequest req)
    {
        var items = await _db.Goods
            .Include(g => g.Author).Include(g => g.Images)
            .Where(g => g.DeletedAt == null && g.Status == GoodsStatus.Active)
            .OrderByDescending(g => g.CreatedAt).Take(200).ToListAsync();

        var dtos = new List<GoodsDto>();
        foreach (var g in items) dtos.Add(await MapAsync(g));
        return new OkObjectResult(dtos);
    }

    [Function("GetGoods")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "goods/{id:guid}")] HttpRequest req, Guid id)
    {
        var g = await _db.Goods.Include(x => x.Author).Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (g is null) return new NotFoundResult();
        return new OkObjectResult(await MapAsync(g));
    }

    /// <summary>Issue short-lived SAS upload URLs; the browser PUTs images directly to Blob.</summary>
    [Function("CreateGoodsUploads")]
    public async Task<IActionResult> Uploads(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "goods/uploads")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var body = await JsonSerializer.DeserializeAsync<UploadRequest>(req.Body);
        if (body is null || body.Files.Count == 0 || body.Files.Count > 8)
            return new BadRequestObjectResult("Provide between 1 and 8 files.");

        var slots = new List<UploadSlot>();
        foreach (var f in body.Files)
        {
            if (!BlobService.AllowedContentTypes.Contains(f.ContentType))
                return new BadRequestObjectResult($"Unsupported content type: {f.ContentType}");
            if (f.SizeBytes <= 0 || f.SizeBytes > BlobService.MaxBytes)
                return new BadRequestObjectResult("Each image must be between 1 byte and 10 MB.");

            var ext = string.IsNullOrWhiteSpace(f.Extension) ? ".jpg" : f.Extension;
            var blobName = _blob.NewBlobName(me.Id, ext);
            var url = await _blob.CreateUploadUrlAsync(blobName, UploadTtl);
            slots.Add(new UploadSlot(blobName, url));
        }
        return new OkObjectResult(slots);
    }

    [Function("CreateGoods")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "goods")] HttpRequest req)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var body = await JsonSerializer.DeserializeAsync<CreateGoodsRequest>(req.Body);
        if (body is null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Description))
            return new BadRequestObjectResult("Title and description are required.");
        if (body.Price < 0) return new BadRequestObjectResult("Price must be >= 0.");
        Enum.TryParse<GoodsCondition>(body.Condition, ignoreCase: true, out var condition);

        var g = new GoodsPost
        {
            Title = body.Title.Trim(),
            Description = body.Description.Trim(),
            Price = body.Price,
            Currency = string.IsNullOrWhiteSpace(body.Currency) ? "EUR" : body.Currency.ToUpperInvariant(),
            Condition = condition,
            Category = body.Category?.Trim(),
            Location = body.Location?.Trim(),
            AuthorId = me.Id
        };
        var order = 0;
        foreach (var blobName in body.BlobNames.Take(8))
            g.Images.Add(new GoodsImage { BlobName = blobName, SortOrder = order++ });

        _db.Goods.Add(g);
        await _db.SaveChangesAsync();
        g.Author = me;
        return new CreatedResult($"/api/goods/{g.Id}", await MapAsync(g));
    }

    [Function("DeleteGoods")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "goods/{id:guid}")] HttpRequest req, Guid id)
    {
        var me = await _users.GetOrCreateUserAsync(req);
        if (me is null) return new UnauthorizedResult();

        var g = await _db.Goods.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (g is null) return new NotFoundResult();
        if (g.AuthorId != me.Id && !CurrentUserService.IsAdmin(me)) return new ForbidResult();

        foreach (var img in g.Images) await _blob.DeleteAsync(img.BlobName);
        g.DeletedAt = DateTimeOffset.UtcNow;
        g.Status = GoodsStatus.Withdrawn;
        await _db.SaveChangesAsync();
        return new NoContentResult();
    }
}
