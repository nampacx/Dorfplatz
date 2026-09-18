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

public sealed class AdminFunctionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AdminFunctionsTests()
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
    public async Task ListUsers_PaginatesAndReturnsTotalCount()
    {
        await using var db = CreateDb();
        await SeedAdminAsync(db);
        await SeedMembersAsync(db, 25, AccessStatus.Approved);

        var result = await CreateSubject(db).ListUsers(CreateRequest("admin-user", "admin@example.com", "?page=2&pageSize=20"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminUsersPageDto>(ok.Value);
        Assert.Equal(26, payload.TotalCount);
        Assert.Equal(2, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.Equal(6, payload.Items.Length);
    }

    [Fact]
    public async Task ListUsers_FiltersBySearchAndStatuses()
    {
        await using var db = CreateDb();
        await SeedAdminAsync(db);
        db.Users.AddRange(
            CreateMember("pending-1", "anna@example.com", "Anna Dorf", AccessStatus.Pending),
            CreateMember("approved-1", "bruno@example.com", "Bruno Nachbar", AccessStatus.Approved),
            CreateMember("approved-2", "julia@example.com", "Suchtreffer", AccessStatus.Approved),
            CreateMember("rejected-1", "anna.blocked@example.com", "Abgelehnt", AccessStatus.Rejected));
        await db.SaveChangesAsync();

        var result = await CreateSubject(db).ListUsers(
            CreateRequest("admin-user", "admin@example.com", "?search=ANNA&statuses=Pending,Rejected"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminUsersPageDto>(ok.Value);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(
            ["anna.blocked@example.com", "anna@example.com"],
            payload.Items.Select(item => item.Email!).ToArray());
    }

    [Fact]
    public async Task ListUsers_ReturnsBadRequestForUnknownStatus()
    {
        await using var db = CreateDb();
        await SeedAdminAsync(db);

        var result = await CreateSubject(db).ListUsers(
            CreateRequest("admin-user", "admin@example.com", "?statuses=Pending,Unknown"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Statuses must be a comma-separated list of Pending, Approved, Rejected or Suspended.",
            badRequest.Value);
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CreateDb() => new(_options);

    private AdminFunctions CreateSubject(AppDbContext db) =>
        new(db, new CurrentUserService(db), new AuditService(db));

    private static AppUser CreateMember(string providerUserId, string email, string displayName, AccessStatus status) =>
        new()
        {
            ProviderUserId = providerUserId,
            Email = email,
            DisplayName = displayName,
            Status = status
        };

    private static HttpRequest CreateRequest(string providerUserId, string email, string queryString = "")
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
        return context.Request;
    }

    private static async Task SeedAdminAsync(AppDbContext db)
    {
        db.Users.Add(new AppUser
        {
            ProviderUserId = "admin-user",
            Email = "admin@example.com",
            DisplayName = "Admin",
            Status = AccessStatus.Approved,
            Roles = new List<UserRole>
            {
                new() { Role = Roles.Admin }
            }
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedMembersAsync(AppDbContext db, int count, AccessStatus status)
    {
        for (var i = 1; i <= count; i++)
        {
            db.Users.Add(CreateMember(
                $"member-{i}",
                $"member{i:00}@example.com",
                $"Mitglied {i:00}",
                status));
        }

        await db.SaveChangesAsync();
    }
}
