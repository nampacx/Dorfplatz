using System.Text;
using System.Text.Json;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BulletinBoard.Api.Tests.Functions;

public sealed class GetRolesFunctionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public GetRolesFunctionTests()
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
    public async Task Run_ReusesExistingAccountWhenProviderUserIdChanges()
    {
        await using var db = CreateDb();
        var existing = new AppUser
        {
            ProviderUserId = "old-provider-id",
            Email = "member@example.com",
            DisplayName = "Member",
            Status = AccessStatus.Approved,
            ConsentedAt = DateTimeOffset.UtcNow,
            Roles = [new UserRole { Role = Roles.Admin }]
        };
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateSubject(db).Run(CreateRequest(
            """{"userId":"new-provider-id","userDetails":"MEMBER@example.com"}"""));

        var ok = Assert.IsType<OkObjectResult>(result);
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Contains(Roles.Admin, response.RootElement.GetProperty("roles")
            .EnumerateArray().Select(role => role.GetString()));

        var users = await db.Users.Include(user => user.Roles).ToListAsync();
        var user = Assert.Single(users);
        Assert.Equal(existing.Id, user.Id);
        Assert.Equal("new-provider-id", user.ProviderUserId);
        Assert.Equal("Member", user.DisplayName);
        Assert.Equal(AccessStatus.Approved, user.Status);
        Assert.NotNull(user.ConsentedAt);
        Assert.Contains(user.Roles, role => role.Role == Roles.Admin);
    }

    [Theory]
    [InlineData("member@example.com", "member")]
    [InlineData(null, "Mitglied")]
    [InlineData("", "Mitglied")]
    [InlineData("@example.com", "Mitglied")]
    public async Task Run_WithoutProviderName_StoresOnlyEmailLocalPart(string? email, string expected)
    {
        await using var db = CreateDb();

        await CreateSubject(db).Run(CreateRequest(JsonSerializer.Serialize(new
        {
            userId = "new-member",
            userDetails = email
        })));

        var user = await db.Users.SingleAsync();
        Assert.Equal(expected, user.DisplayName);
        Assert.Equal(email, user.Email);
        Assert.DoesNotContain("@", user.DisplayName!);
    }

    [Theory]
    [InlineData("name", "Member Name", "Member Name")]
    [InlineData("name", "member@example.com", "member")]
    [InlineData("given_name", "member@example.com", "member")]
    [InlineData("given_name", "Member", "Member")]
    public async Task Run_WithProviderClaim_RejectsEmailNames(string claimType, string claimValue, string expected)
    {
        await using var db = CreateDb();

        await CreateSubject(db).Run(CreateRequest(JsonSerializer.Serialize(new
        {
            userId = "new-member",
            userDetails = "member@example.com",
            claims = new[] { new { typ = claimType, val = claimValue } }
        })));

        Assert.Equal(expected, (await db.Users.SingleAsync()).DisplayName);
    }

    [Theory]
    [InlineData("member@example.com", "member")]
    [InlineData("MEMBER@example.com", "MEMBER")]
    [InlineData(null, "member")]
    [InlineData(" ", "member")]
    [InlineData("Pseudonym", "Pseudonym")]
    public async Task Run_ExistingAccount_RepairsLegacyNameAndPreservesPseudonym(string? name, string expected)
    {
        await using var db = CreateDb();
        db.Users.Add(new AppUser
        {
            ProviderUserId = "member",
            Email = "member@example.com",
            DisplayName = name
        });
        await db.SaveChangesAsync();

        await CreateSubject(db).Run(CreateRequest(
            """{"userId":"member","userDetails":"member@example.com"}"""));

        Assert.Equal(expected, (await db.Users.SingleAsync()).DisplayName);
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CreateDb() => new(_options);

    private static GetRolesFunction CreateSubject(AppDbContext db) =>
        new(db, NullLogger<GetRolesFunction>.Instance);

    private static HttpRequest CreateRequest(string payload)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        return context.Request;
    }
}
