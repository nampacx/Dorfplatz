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
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public AccountFunctionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(AccessStatus.Pending)]
    [InlineData(AccessStatus.Approved)]
    [InlineData(AccessStatus.Rejected)]
    [InlineData(AccessStatus.Suspended)]
    public async Task Export_IncludesAllProfileAndRoleFieldsRegardlessOfApproval(AccessStatus status)
    {
        // Arrange
        await using var db = CreateDb();
        var me = CreateUser("caller");
        me.Status = status;
        me.StatusReason = "Status explanation";
        me.CreatedAt = Timestamp;
        me.ConsentedAt = Timestamp.AddDays(1);
        me.DecidedAt = Timestamp.AddDays(2);
        me.DecidedByUserId = Guid.NewGuid();
        var role = new UserRole
        {
            User = me, Role = Roles.Moderator,
            GrantedAt = Timestamp.AddDays(3), GrantedByUserId = me.DecidedByUserId
        };
        me.Roles.Add(role);
        db.Users.Add(me);
        await db.SaveChangesAsync();

        // Act
        using var export = await ExportAsync(db);

        // Assert
        var root = export.RootElement;
        AssertStoredFields(db, me, root.GetProperty("profile"));
        AssertStoredFields(db, role, Assert.Single(root.GetProperty("profile").GetProperty("Roles").EnumerateArray()));
        Assert.Equal(new[] { Roles.Moderator, Roles.User },
            root.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).OrderBy(r => r).ToArray());
        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("gdpr.export", audit.Action);
        Assert.Equal(me.Id, audit.ActorUserId);
    }

    [Fact]
    public async Task Export_IncludesAllOwnListingFieldsAndImagesIncludingSoftDeletedPosts()
    {
        // Arrange
        await using var db = CreateDb();
        var me = CreateUser("caller");
        var other = CreateUser("other");
        var ev = new EventPost
        {
            Author = me, Title = "Event", Description = "Event description",
            EventDate = Timestamp.AddDays(5), EndDate = Timestamp.AddDays(6),
            Location = "Village hall", Category = EventCategory.Vereine,
            ExternalImageUrl = "https://example.com/event.jpg", ImageBlobName = "caller/event.jpg",
            Status = EventStatus.Rejected, RejectionReason = "Needs revision",
            ModeratedById = other.Id, ModeratedAt = Timestamp.AddDays(2),
            CreatedAt = Timestamp, UpdatedAt = Timestamp.AddDays(3), DeletedAt = Timestamp.AddDays(4)
        };
        var service = new ServicePost
        {
            Author = me, Type = ServiceType.Offering, Title = "Service", Description = "Service description",
            Category = "Garden", Status = ServiceStatus.Closed,
            CreatedAt = Timestamp, UpdatedAt = Timestamp.AddDays(1), DeletedAt = Timestamp.AddDays(2)
        };
        var goods = new GoodsPost
        {
            Author = me, Title = "Goods", Description = "Goods description",
            Price = 12.50m, Currency = "EUR", Condition = GoodsCondition.Good,
            Category = "Tools", Location = "Asch", Status = GoodsStatus.Withdrawn,
            CreatedAt = Timestamp, UpdatedAt = Timestamp.AddDays(1), DeletedAt = Timestamp.AddDays(2),
            Images = new List<GoodsImage>
            {
                new() { BlobName = "caller/second.jpg", SortOrder = 1 },
                new() { BlobName = "caller/first.jpg", SortOrder = 0 }
            }
        };
        db.AddRange(me, other, ev, service, goods,
            new EventPost { Author = other, Title = "Private event", Description = "Other data" },
            new ServicePost { Author = other, Title = "Private service", Description = "Other data" },
            new GoodsPost
            {
                Author = other, Title = "Private goods", Description = "Other data",
                Images = new List<GoodsImage> { new() { BlobName = "other/private.jpg" } }
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        using var export = await ExportAsync(db);

        // Assert
        var root = export.RootElement;
        AssertStoredFields(db, ev, Assert.Single(root.GetProperty("events").EnumerateArray()));
        AssertStoredFields(db, service, Assert.Single(root.GetProperty("services").EnumerateArray()));
        var exportedGoods = Assert.Single(root.GetProperty("goods").EnumerateArray());
        AssertStoredFields(db, goods, exportedGoods);
        var images = exportedGoods.GetProperty("Images").EnumerateArray().ToArray();
        Assert.Equal(2, images.Length);
        AssertStoredFields(db, goods.Images.Single(i => i.SortOrder == 0), images[0]);
        AssertStoredFields(db, goods.Images.Single(i => i.SortOrder == 1), images[1]);
        Assert.DoesNotContain("Other data", root.GetRawText());
        Assert.DoesNotContain("other/private.jpg", root.GetRawText());
    }

    [Fact]
    public async Task Export_IncludesOwnConversationsAndSentAndReceivedMessagesButNotOtherUsersPrivateData()
    {
        // Arrange
        await using var db = CreateDb();
        var me = CreateUser("caller");
        var other = CreateUser("other");
        var third = CreateUser("third");
        var conversation = new Conversation
        {
            ParticipantA = me, ParticipantB = other, ListingType = "goods", ListingId = Guid.NewGuid(),
            CreatedAt = Timestamp, LastMessageAt = Timestamp.AddHours(2)
        };
        var emptyConversation = new Conversation
        {
            ParticipantA = third, ParticipantB = me,
            CreatedAt = Timestamp.AddDays(1), LastMessageAt = Timestamp.AddDays(1)
        };
        var privateConversation = new Conversation { ParticipantA = other, ParticipantB = third };
        var sent = new Message
        {
            Conversation = conversation, Sender = me, Body = "Sent message",
            SentAt = Timestamp.AddHours(1), ReadAt = Timestamp.AddHours(2)
        };
        var received = new Message
        {
            Conversation = conversation, Sender = other, Body = "Received message",
            SentAt = Timestamp.AddHours(2)
        };
        var deleted = new Message
        {
            Conversation = conversation, Sender = other, Body = "", IsDeleted = true,
            SentAt = Timestamp.AddMinutes(30)
        };
        db.AddRange(me, other, third, conversation, emptyConversation, privateConversation, sent, received, deleted,
            new Message { Conversation = privateConversation, Sender = other, Body = "Private message" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        using var export = await ExportAsync(db);

        // Assert
        var root = export.RootElement;
        var conversations = root.GetProperty("conversations").EnumerateArray().ToArray();
        Assert.Equal(2, conversations.Length);
        foreach (var (entity, partner) in new[] { (conversation, other), (emptyConversation, third) })
        {
            var item = conversations.Single(c => c.GetProperty("Id").GetGuid() == entity.Id);
            AssertStoredFields(db, entity, item);
            Assert.Equal(partner.Id, item.GetProperty("OtherUserId").GetGuid());
            Assert.Equal(partner.DisplayName, item.GetProperty("OtherDisplayName").GetString());
        }
        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(3, messages.Length);
        foreach (var message in new[] { sent, received, deleted })
            AssertStoredFields(db, message, messages.Single(m => m.GetProperty("Id").GetGuid() == message.Id));
        Assert.DoesNotContain("Private message", root.GetRawText());
        Assert.DoesNotContain(other.Email!, root.GetRawText());
        Assert.DoesNotContain(third.Email!, root.GetRawText());
        Assert.DoesNotContain(other.ProviderUserId, root.GetRawText());
        Assert.DoesNotContain(privateConversation.Id.ToString(), root.GetRawText());
    }

    [Fact]
    public async Task Export_IncludesAuditEntriesByActorAccountTargetAndOwnEventButExcludesUnrelatedEntries()
    {
        // Arrange
        await using var db = CreateDb();
        var me = CreateUser("caller");
        var other = CreateUser("other");
        var ev = new EventPost
        {
            Author = me, Title = "Deleted event", Description = "Description", DeletedAt = Timestamp
        };
        var otherEvent = new EventPost { Author = other, Title = "Other event", Description = "Description" };
        var relevant = new[]
        {
            new AuditLog { Action = "gdpr.consent", ActorUserId = me.Id, CreatedAt = Timestamp },
            new AuditLog
            {
                Action = "role.grant", ActorUserId = other.Id, TargetType = "user", TargetId = me.Id,
                Detail = "{\"role\":\"moderator\"}", CreatedAt = Timestamp.AddDays(1)
            },
            new AuditLog
            {
                Action = "member.suspend", ActorUserId = other.Id, TargetType = "user", TargetId = me.Id,
                Detail = "{\"reason\":\"Explanation\"}", CreatedAt = Timestamp.AddDays(2)
            },
            new AuditLog
            {
                Action = "event.reject", ActorUserId = other.Id, TargetType = "event", TargetId = ev.Id,
                Detail = "{\"reason\":\"Needs revision\"}", CreatedAt = Timestamp.AddDays(3)
            },
            new AuditLog
            {
                Action = "role.revoke", ActorUserId = me.Id, TargetType = "user", TargetId = other.Id,
                Detail = "{\"role\":\"admin\"}", CreatedAt = Timestamp.AddDays(4)
            }
        };
        db.AddRange(me, other, ev, otherEvent);
        db.AuditLogs.AddRange(relevant);
        db.AuditLogs.AddRange(
            new AuditLog { Action = "role.grant", ActorUserId = other.Id, TargetType = "user", TargetId = other.Id },
            new AuditLog { Action = "event.reject", ActorUserId = other.Id, TargetType = "event", TargetId = otherEvent.Id },
            new AuditLog { Action = "unrelated", TargetType = "event", TargetId = me.Id },
            new AuditLog { Action = "unrelated", TargetType = "user", TargetId = ev.Id },
            new AuditLog { Action = "unrelated" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        using var export = await ExportAsync(db);

        // Assert
        var logs = export.RootElement.GetProperty("auditLogs").EnumerateArray().ToArray();
        Assert.Equal(relevant.Length, logs.Length);
        foreach (var log in relevant)
            AssertStoredFields(db, log, logs.Single(a => a.GetProperty("Id").GetGuid() == log.Id));
    }

    [Fact]
    public async Task Export_AnonymousRequestReturnsUnauthorizedAndDoesNotWriteAuditEntry()
    {
        // Arrange
        await using var db = CreateDb();
        var request = new DefaultHttpContext().Request;

        // Act
        var result = await CreateSubject(db).Export(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("member@example.com")]
    [InlineData("Name <other@example.com>")]
    [InlineData("@")]
    public async Task UpdateProfile_EmailLikeName_IsRejected(string name)
    {
        await using var db = CreateDb();
        var member = CreateUser("member");
        member.DisplayName = "Pseudonym";
        db.Users.Add(member);
        await db.SaveChangesAsync();

        var result = await CreateSubject(db).UpdateProfile(CreateRequest(name));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Pseudonym", (await db.Users.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_Pseudonym_IsSavedAndReturnsOwnEmail()
    {
        await using var db = CreateDb();
        var member = CreateUser("member");
        db.Users.Add(member);
        await db.SaveChangesAsync();

        var result = await CreateSubject(db).UpdateProfile(CreateRequest("  Dorfkind  "));

        var profile = Assert.IsType<MeDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Dorfkind", profile.DisplayName);
        Assert.Equal("member@example.com", profile.Email);
        Assert.Equal(member.Id, profile.Id);
        Assert.Equal("Dorfkind", (await db.Users.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task Me_Anonymous_CannotReadEmail()
    {
        await using var db = CreateDb();
        Assert.IsType<UnauthorizedResult>(await CreateSubject(db).Me(new DefaultHttpContext().Request));
    }

    private static HttpRequest CreateRequest(string name)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers["x-ms-client-principal"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new ClientPrincipal { UserId = "provider-member", UserDetails = "member@example.com" })));
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { displayName = name })));
        return request;
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CreateDb() => new(_options);

    private static AccountFunctions CreateSubject(AppDbContext db) =>
        new(db, new CurrentUserService(db), new AuditService(db), new UnusedBlobService());

    private static AppUser CreateUser(string name) => new()
    {
        ProviderUserId = $"provider-{name}", Email = $"{name}@example.com", DisplayName = name
    };

    private static async Task<JsonDocument> ExportAsync(AppDbContext db)
    {
        var principal = new ClientPrincipal
        {
            IdentityProvider = "aad", UserId = "provider-caller",
            UserDetails = "caller@example.com", UserRoles = ["authenticated"]
        };
        var request = new DefaultHttpContext().Request;
        request.Headers["x-ms-client-principal"] =
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));
        var result = Assert.IsType<FileContentResult>(await CreateSubject(db).Export(request));
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("my-bulletinboard-data.json", result.FileDownloadName);
        return JsonDocument.Parse(result.FileContents);
    }

    private static void AssertStoredFields<T>(AppDbContext db, T entity, JsonElement exported) where T : class
    {
        foreach (var property in db.Model.FindEntityType(typeof(T))!.GetProperties())
        {
            var expected = property.PropertyInfo!.GetValue(entity);
            var actual = exported.GetProperty(property.Name).Deserialize(property.ClrType);
            Assert.Equal(expected, actual);
        }
    }

    private sealed class UnusedBlobService : BlobService { }
}
