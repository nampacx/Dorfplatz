using System.Text;
using System.Text.Json;
using BulletinBoard.Api.Auth;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Functions;
using BulletinBoard.Api.Models;
using BulletinBoard.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;

namespace BulletinBoard.Api.Tests.Functions;

public sealed class EventsFunctionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public EventsFunctionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Create_RejectsExternalImageUrl()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");

        var result = await CreateSubject(db).Create(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new
            {
                title = "Sommerfest",
                description = "Am Dorfplatz",
                category = "Vereine",
                imageUrl = " https://example.com/flyer.png "
            }));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("External image URLs are not supported. Upload an image instead.", badRequest.Value);
        Assert.Empty(await db.Events.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsConflictingImageSources()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");

        var result = await CreateSubject(db).Create(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new
            {
                title = "Sommerfest",
                description = "Am Dorfplatz",
                imageUrl = "https://example.com/flyer.png",
                imageBlobName = $"{member.Id}/flyer.png"
            }));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Provide either an image URL or an uploaded image, not both.", badRequest.Value);
    }

    [Fact]
    public async Task Create_ReturnsSignedImageUrlForUploadedImage()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");
        var blob = new FakeBlobService();
        var result = await CreateSubject(db, blob).Create(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new
            {
                title = "Sommerfest",
                description = "Am Dorfplatz",
                category = "Vereine",
                imageBlobName = $"{member.Id}/flyer.png"
            }));

        var created = Assert.IsType<CreatedResult>(result);
        var payload = Assert.IsType<EventDto>(created.Value);
        Assert.Equal($"https://cdn.example/{member.Id}/flyer.png", payload.ImageUrl);
        Assert.Equal($"{member.Id}/flyer.png", payload.ImageBlobName);
    }

    [Fact]
    public async Task Update_ReplacingUploadedImage_DeletesOldBlob()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");
        var existing = new EventPost
        {
            Title = "Sommerfest",
            Description = "Alt",
            Category = EventCategory.Vereine,
            AuthorId = member.Id,
            Status = EventStatus.Published,
            ImageBlobName = $"{member.Id}/old.png"
        };
        db.Events.Add(existing);
        await db.SaveChangesAsync();

        var blob = new FakeBlobService();
        var result = await CreateSubject(db, blob).Update(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new
            {
                title = "Sommerfest",
                description = "Neu",
                category = "Vereine",
                imageBlobName = $"{member.Id}/new.png"
            }),
            existing.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<EventDto>(ok.Value);
        Assert.Equal($"https://cdn.example/{member.Id}/new.png", payload.ImageUrl);
        Assert.Equal($"{member.Id}/new.png", payload.ImageBlobName);
        Assert.Contains($"{member.Id}/old.png", blob.DeletedBlobNames);

        var stored = await db.Events.SingleAsync();
        Assert.Null(stored.ExternalImageUrl);
        Assert.Equal($"{member.Id}/new.png", stored.ImageBlobName);
        Assert.Equal(EventStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task Update_RejectsExternalImageUrl()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");
        var existing = new EventPost
        {
            Title = "Sommerfest",
            Description = "Alt",
            AuthorId = member.Id,
            Status = EventStatus.Pending
        };
        db.Events.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateSubject(db).Update(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new
            {
                title = "Sommerfest",
                description = "Neu",
                imageUrl = "https://example.com/new.png"
            }),
            existing.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("External image URLs are not supported. Upload an image instead.", badRequest.Value);
        Assert.Null(existing.ExternalImageUrl);
    }

    [Fact]
    public async Task Update_DoesNotExposeLegacyExternalImageUrl()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");
        var existing = new EventPost
        {
            Title = "Sommerfest",
            Description = "Alt",
            AuthorId = member.Id,
            Status = EventStatus.Pending,
            ExternalImageUrl = "https://example.com/tracking-image.png"
        };
        db.Events.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateSubject(db).Update(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new { title = "Sommerfest", description = "Neu" }),
            existing.Id);

        var response = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<EventDto>(response.Value);
        Assert.Null(payload.ImageUrl);
        Assert.Null(existing.ExternalImageUrl);
    }

    [Fact]
    public async Task Uploads_RejectsFilesOver512Kilobytes()
    {
        await using var db = CreateDb();
        var member = await SeedApprovedMemberAsync(db, "member-1", "member1@example.com");

        var result = await CreateSubject(db).Uploads(CreateRequest(
            member.ProviderUserId,
            member.Email!,
            new
            {
                files = new[]
                {
                    new { contentType = "image/png", sizeBytes = 600 * 1024, extension = ".png" }
                }
            }));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Event images must be smaller than 512 KB.", badRequest.Value);
    }

    [Theory]
    [InlineData(null, AccessStatus.Approved, false)]
    [InlineData(Roles.User, AccessStatus.Approved, false)]
    [InlineData(Roles.Moderator, AccessStatus.Approved, true)]
    [InlineData(Roles.Admin, AccessStatus.Approved, true)]
    [InlineData(Roles.Moderator, AccessStatus.Pending, false)]
    [InlineData(Roles.Admin, AccessStatus.Rejected, false)]
    [InlineData(Roles.Moderator, AccessStatus.Suspended, false)]
    [InlineData(Roles.Admin, AccessStatus.Suspended, false)]
    public async Task List_OnlyApprovedModeratorsAndAdminsSeeSubmissionInfo(
        string? role, AccessStatus status, bool canSee)
    {
        await using var db = CreateDb();
        var author = await new CurrentUserService(db).GetOrCreateUserAsync(
            CreateRequest("author", "author@example.com"));
        db.Events.AddRange(
            new EventPost { Title = "Current", Description = "Current event", AuthorId = author!.Id, Status = EventStatus.Published },
            new EventPost
            {
                Title = "Archived", Description = "Archived event", AuthorId = author.Id, Status = EventStatus.Published,
                EventDate = DateTimeOffset.UtcNow.AddYears(-1)
            });
        var request = new DefaultHttpContext().Request;
        if (role is not null)
        {
            var viewer = new AppUser
            {
                ProviderUserId = "viewer", Email = "viewer@example.com", Status = status,
                Roles = [new UserRole { Role = role }]
            };
            db.Users.Add(viewer);
            request = CreateRequest("viewer", "viewer@example.com");
        }
        request.QueryString = new QueryString("?includePast=true");
        await db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await CreateSubject(db).List(request));
        var events = Assert.IsType<EventDto[]>(result.Value);

        Assert.Equal(canSee ? 2 : 1, events.Length);
        Assert.All(events, item =>
        {
            Assert.Equal(canSee ? "author" : null, item.AuthorName);
            Assert.Equal(canSee, item.CreatedAt.HasValue);
            Assert.DoesNotContain("@", item.AuthorName ?? "");
        });
        Assert.DoesNotContain("author@example.com", JsonSerializer.Serialize(events));
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CreateDb() => new SqliteTestDbContext(_options);

    private EventsFunctions CreateSubject(AppDbContext db, BlobService? blob = null) =>
        new(db, new CurrentUserService(db), new AuditService(db), blob ?? new FakeBlobService());

    private static HttpRequest CreateRequest(string providerUserId, string email, object? body = null, string queryString = "")
    {
        var principal = new ClientPrincipal
        {
            IdentityProvider = "aad",
            UserId = providerUserId,
            UserDetails = email,
            UserRoles = ["authenticated"]
        };

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] =
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));
        context.Request.QueryString = new QueryString(queryString);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body);
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Request.ContentType = "application/json";
        }

        return context.Request;
    }

    private static async Task<AppUser> SeedApprovedMemberAsync(AppDbContext db, string providerUserId, string email)
    {
        var member = new AppUser
        {
            ProviderUserId = providerUserId,
            Email = email,
            DisplayName = "Mitglied",
            Status = AccessStatus.Approved
        };
        db.Users.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    private sealed class SqliteTestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // SQLite needs a numeric mapping to compare and sort the UTC timestamps in list tests.
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    private sealed class FakeBlobService : BlobService
    {
        public List<string> DeletedBlobNames { get; } = [];

        public override Task<string> CreateUploadUrlAsync(string blobName, TimeSpan validFor) =>
            Task.FromResult($"https://upload.example/{blobName}");

        public override Task<string> CreateReadUrlAsync(string blobName, TimeSpan validFor) =>
            Task.FromResult($"https://cdn.example/{blobName}");

        public override Task DeleteAsync(string blobName)
        {
            DeletedBlobNames.Add(blobName);
            return Task.CompletedTask;
        }
    }
}
