using System.Text;
using System.Text.Json;
using BulletinBoard.Api.Auth;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BulletinBoard.Api.Tests.Services;

public sealed class CurrentUserServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CurrentUserServiceTests()
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
    public async Task GetOrCreateUserAsync_ReusesExistingAccountWhenProviderUserIdChanges()
    {
        await using var db = CreateDb();
        var existing = new AppUser
        {
            ProviderUserId = "old-provider-id",
            Email = "member@example.com",
            DisplayName = "Member",
            Status = AccessStatus.Approved
        };
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var user = await new CurrentUserService(db).GetOrCreateUserAsync(
            CreateRequest("new-provider-id", "MEMBER@example.com"));

        Assert.NotNull(user);
        Assert.Equal(existing.Id, user.Id);
        Assert.Equal("new-provider-id", user.ProviderUserId);
        Assert.Equal("Member", user.DisplayName);
        Assert.Equal(AccessStatus.Approved, user.Status);
        Assert.Single(await db.Users.ToListAsync());
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CreateDb() => new(_options);

    private static HttpRequest CreateRequest(string userId, string email)
    {
        var principal = new ClientPrincipal
        {
            IdentityProvider = "aad",
            UserId = userId,
            UserDetails = email,
            UserRoles = ["authenticated"]
        };

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] =
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));
        return context.Request;
    }
}
