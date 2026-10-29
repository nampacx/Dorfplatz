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
    private readonly AppDbContext _db;
    private readonly AppUser _member;

    public AccountFunctionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _member = new AppUser
        {
            ProviderUserId = "member", Email = "member@example.com", DisplayName = "Pseudonym"
        };
        _db.Users.Add(_member);
        _db.SaveChanges();
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

    private AccountFunctions CreateSubject() =>
        new(_db, new CurrentUserService(_db), new AuditService(_db), new FakeBlobService());

    private static HttpRequest CreateRequest(string name)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers["x-ms-client-principal"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new ClientPrincipal { UserId = "member", UserDetails = "member@example.com" })));
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { displayName = name })));
        return request;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeBlobService : BlobService { }
}
