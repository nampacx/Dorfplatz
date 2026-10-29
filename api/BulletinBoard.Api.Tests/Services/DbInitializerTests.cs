using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using BulletinBoard.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BulletinBoard.Api.Tests.Services;

public sealed class DbInitializerTests
{
    private static readonly string[] MigrationIds =
    [
        "20260811082546_InitialCreate",
        "20260811125425_AddEventCategory",
        "20260813133353_AddMemberApproval",
        "20260903081503_AddMemberSuspension",
        "20260916170000_AddEventEndDate",
        "20260918134000_AddEventImages"
    ];

    [Fact]
    public async Task InitializeAsync_WhenMigrationFails_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=/definitely/missing/bulletinboard.sqlite"));

        await using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAnyAsync<Exception>(() => DbInitializer.InitializeAsync(provider));
    }

    [Fact]
    public async Task InitializeAsync_CleansLegacyNamesAndSeedsSafeAdminName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:AdminEmail"] = "admin@example.com" })
            .Build());
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            await SeedMigrationHistoryAsync(db);
            foreach (var name in new[] { "member@example.com", " OTHER@example.com ", "@example.com", "Pseudonym", "Deleted user" })
                db.Users.Add(new AppUser { ProviderUserId = name, Email = "private@example.com", DisplayName = name });
            await db.SaveChangesAsync();
        }

        await DbInitializer.InitializeAsync(provider);
        await DbInitializer.InitializeAsync(provider);

        using var verifyScope = provider.CreateScope();
        var users = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Users.ToListAsync();
        Assert.Equal(6, users.Count);
        Assert.All(users, user => Assert.DoesNotContain("@", user.DisplayName!));
        Assert.Equal("member", users.Single(u => u.ProviderUserId == "member@example.com").DisplayName);
        Assert.Equal("OTHER", users.Single(u => u.ProviderUserId == " OTHER@example.com ").DisplayName);
        Assert.Equal("Mitglied", users.Single(u => u.ProviderUserId == "@example.com").DisplayName);
        Assert.Equal("Pseudonym", users.Single(u => u.ProviderUserId == "Pseudonym").DisplayName);
        Assert.Equal("Deleted user", users.Single(u => u.ProviderUserId == "Deleted user").DisplayName);
        Assert.Equal("admin", users.Single(u => u.Email == "admin@example.com").DisplayName);
        Assert.Equal("private@example.com", users.Single(u => u.ProviderUserId == "member@example.com").Email);
    }

    [Fact]
    public async Task InitializeAsync_WhenEventImageColumnsAreMissing_RepairsSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Events" DROP COLUMN "ExternalImageUrl";""");
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Events" DROP COLUMN "ImageBlobName";""");
            await SeedMigrationHistoryAsync(db);
        }

        await DbInitializer.InitializeAsync(provider);

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var columns = await GetSqliteColumnNamesAsync(verifyDb, "Events");
        Assert.Contains("ExternalImageUrl", columns);
        Assert.Contains("ImageBlobName", columns);
    }

    private static async Task<HashSet<string>> GetSqliteColumnNamesAsync(AppDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{tableName}");""";

            var columns = new HashSet<string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(1));

            return columns;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private static async Task SeedMigrationHistoryAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        foreach (var migrationId in MigrationIds)
        {
            await db.Database.ExecuteSqlRawAsync(
                """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ({0}, '8.0.10');""",
                migrationId);
        }
    }
}
