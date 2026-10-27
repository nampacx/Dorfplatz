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
