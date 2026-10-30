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
using Xunit;

namespace BulletinBoard.Api.Tests.Functions;

public sealed class AccountFunctionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly AppDbContext _db;
    private readonly AppUser _member;

    public AccountFunctionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(_options);
        _db.Database.EnsureCreated();
        _member = new AppUser
        {
            ProviderUserId = "member",
            Email = "member@example.com",
            DisplayName = "Pseudonym"
        };
        _db.Users.Add(_member);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Delete_ClearsAuthoredContentAndPersonalAuditDetails()
    {
        await using var db = CreateDb();
        var data = await SeedAccountDataAsync(db);

        var result = await CreateSubject(db, data.Blob)
            .Delete(CreatePrincipalRequest(data.Member.ProviderUserId));

        Assert.IsType<OkObjectResult>(result);

        var member = await db.Users.SingleAsync(u => u.Id == data.Member.Id);
        Assert.NotEqual("provider-user-id", member.ProviderUserId);
        Assert.Null(member.Email);
        Assert.Equal("Deleted user", member.DisplayName);
        Assert.Null(member.ConsentedAt);
        Assert.Null(member.StatusReason);
        Assert.Null(member.DecidedAt);
        Assert.Null(member.DecidedByUserId);
        Assert.NotNull(member.DeletedAt);

        var eventPost = await db.Events.SingleAsync();
        Assert.Equal(string.Empty, eventPost.Title);
        Assert.Equal(string.Empty, eventPost.Description);
        Assert.Null(eventPost.EventDate);
        Assert.Null(eventPost.EndDate);
        Assert.Null(eventPost.Location);
        Assert.Null(eventPost.ExternalImageUrl);
        Assert.Null(eventPost.ImageBlobName);
        Assert.Null(eventPost.RejectionReason);
        Assert.NotNull(eventPost.DeletedAt);

        var service = await db.Services.SingleAsync();
        Assert.Equal(string.Empty, service.Title);
        Assert.Equal(string.Empty, service.Description);
        Assert.Null(service.Category);
        Assert.NotNull(service.DeletedAt);

        var goods = await db.Goods.SingleAsync();
        Assert.Equal(string.Empty, goods.Title);
        Assert.Equal(string.Empty, goods.Description);
        Assert.Null(goods.Category);
        Assert.Null(goods.Location);
        Assert.Equal(0, goods.Price);
        Assert.NotNull(goods.DeletedAt);
        Assert.Empty(await db.GoodsImages.ToListAsync());

        var message = await db.Messages.SingleAsync();
        Assert.Equal(string.Empty, message.Body);
        Assert.True(message.IsDeleted);

        Assert.All(await db.AuditLogs.ToListAsync(), log => Assert.Null(log.Detail));
    }

    [Fact]
    public async Task Delete_RemovesEventAndGoodsImageBlobs()
    {
        await using var db = CreateDb();
        var data = await SeedAccountDataAsync(db);

        await CreateSubject(db, data.Blob)
            .Delete(CreatePrincipalRequest(data.Member.ProviderUserId));

        Assert.Equal(
            new[] { data.EventBlobName, data.GoodsBlobName },
            data.Blob.DeletedBlobNames);
    }

    [Theory]
    [InlineData("member@example.com")]
    [InlineData("Name <other@example.com>")]
    [InlineData("@")]
    public async Task UpdateProfile_EmailLikeName_IsRejected(string name)
    {
        var result = await CreateSubject().UpdateProfile(CreateRequest(name));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Pseudonym", (await _db.Users.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_Pseudonym_IsSavedAndReturnsOwnEmail()
    {
        var result = await CreateSubject().UpdateProfile(CreateRequest("  Dorfkind  "));

        var profile = Assert.IsType<MeDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Dorfkind", profile.DisplayName);
        Assert.Equal("member@example.com", profile.Email);
        Assert.Equal(_member.Id, profile.Id);
        Assert.Equal("Dorfkind", (await _db.Users.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task Me_Anonymous_CannotReadEmail()
    {
        Assert.IsType<UnauthorizedResult>(await CreateSubject().Me(new DefaultHttpContext().Request));
    }

    private AppDbContext CreateDb() => new(_options);

    private AccountFunctions CreateSubject() =>
        new(_db, new CurrentUserService(_db), new AuditService(_db), new FakeBlobService());

    private static AccountFunctions CreateSubject(AppDbContext db, FakeBlobService blob) =>
        new(db, new CurrentUserService(db), new AuditService(db), blob);

    private static HttpRequest CreateRequest(string name)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers["x-ms-client-principal"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new ClientPrincipal { UserId = "member", UserDetails = "member@example.com" })));
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { displayName = name })));
        return request;
    }

    private static HttpRequest CreatePrincipalRequest(string providerUserId)
    {
        var principal = new ClientPrincipal
        {
            IdentityProvider = "aad",
            UserId = providerUserId,
            UserDetails = "member@example.com",
            UserRoles = ["authenticated"]
        };
        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] =
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));
        return context.Request;
    }

    private static async Task<AccountData> SeedAccountDataAsync(AppDbContext db)
    {
        var member = new AppUser
        {
            ProviderUserId = "provider-user-id",
            Email = "member@example.com",
            DisplayName = "Mitglied",
            Status = AccessStatus.Suspended,
            StatusReason = "Persönlicher Ablehnungsgrund",
            ConsentedAt = DateTimeOffset.UtcNow,
            DecidedAt = DateTimeOffset.UtcNow,
            DecidedByUserId = Guid.NewGuid()
        };
        var otherMember = new AppUser
        {
            ProviderUserId = "other-provider-id",
            Email = "other@example.com",
            DisplayName = "Anderes Mitglied",
            Status = AccessStatus.Approved
        };
        db.Users.AddRange(member, otherMember);

        const string eventBlobName = "event-image.png";
        const string goodsBlobName = "goods-image.png";
        var eventPost = new EventPost
        {
            Title = "Persönlicher Eventtitel",
            Description = "Persönlicher Eventtext",
            EventDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(1),
            Location = "Private Adresse",
            ExternalImageUrl = "https://example.com/private.png",
            ImageBlobName = eventBlobName,
            Author = member,
            RejectionReason = "Persönlicher Ablehnungsgrund"
        };
        var service = new ServicePost
        {
            Title = "Persönlicher Dienstleistungstitel",
            Description = "Persönlicher Dienstleistungstext",
            Category = "Persönliche Kategorie",
            Author = member
        };
        var goods = new GoodsPost
        {
            Title = "Persönlicher Anzeigentitel",
            Description = "Persönlicher Anzeigentext",
            Price = 10,
            Category = "Persönliche Kategorie",
            Location = "Private Adresse",
            Author = member,
            Images = [new GoodsImage { BlobName = goodsBlobName }]
        };
        var conversation = new Conversation
        {
            ParticipantA = member,
            ParticipantB = otherMember
        };
        db.Events.Add(eventPost);
        db.Services.Add(service);
        db.Goods.Add(goods);
        db.Messages.Add(new Message
        {
            Conversation = conversation,
            Sender = member,
            Body = "Persönlicher Nachrichtentext"
        });
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Action = "event.reject",
                ActorUserId = otherMember.Id,
                TargetType = "event",
                TargetId = eventPost.Id,
                Detail = """{"reason":"Persönlicher Ablehnungsgrund"}"""
            },
            new AuditLog
            {
                Action = "message.report",
                ActorUserId = member.Id,
                Detail = """{"text":"Persönlicher Audittext"}"""
            });
        await db.SaveChangesAsync();

        return new AccountData(member, new FakeBlobService(), eventBlobName, goodsBlobName);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed record AccountData(
        AppUser Member,
        FakeBlobService Blob,
        string EventBlobName,
        string GoodsBlobName);

    private sealed class FakeBlobService : BlobService
    {
        public List<string> DeletedBlobNames { get; } = [];

        public override Task DeleteAsync(string blobName)
        {
            DeletedBlobNames.Add(blobName);
            return Task.CompletedTask;
        }
    }
}
