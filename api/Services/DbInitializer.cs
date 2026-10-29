using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BulletinBoard.Api.Services;

/// <summary>
/// Applies migrations at startup and (optionally) seeds a first admin from Seed:AdminEmail.
/// </summary>
public static class DbInitializer
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    public static async Task InitializeAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            await RepairEventImageColumnsAsync(db, logger);
            await db.Database.MigrateAsync();
            await db.Users
                .Where(u => u.DisplayName != null && u.DisplayName.Contains("@"))
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.DisplayName,
                    u => u.DisplayName!.Substring(0, u.DisplayName.IndexOf("@")).Trim() == ""
                        ? "Mitglied"
                        : u.DisplayName.Substring(0, u.DisplayName.IndexOf("@")).Trim()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed at startup.");
            throw;
        }

        var adminEmail = config["Seed:AdminEmail"];
        if (string.IsNullOrWhiteSpace(adminEmail)) return;

        // Ensure a bootstrap admin exists (matched by email once they first sign in).
        var existing = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (existing is null)
        {
            // Pre-provision so GetRoles grants admin on the very first login.
            existing = new AppUser
            {
                ProviderUserId = $"seed:{adminEmail}",
                Email = adminEmail,
                DisplayName = CurrentUserService.SafeDisplayName(adminEmail),
                // The bootstrap admin approves everyone else, so they cannot be
                // waiting on an approval themselves.
                Status = AccessStatus.Approved,
                DecidedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(existing);
        }

        if (!existing.Roles.Any(r => r.Role == Roles.Admin))
        {
            // Via the DbSet, not existing.Roles — see AdminFunctions.UpdateRoles. When
            // the user is already in the database, adding through the navigation makes
            // EF update a row that isn't there instead of inserting one.
            db.UserRoles.Add(new UserRole { Role = Roles.Admin, UserId = existing.Id });
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded admin role for {Email}", adminEmail);
        }
    }

    private static async Task RepairEventImageColumnsAsync(AppDbContext db, ILogger logger)
    {
        var provider = db.Database.ProviderName;
        if (provider == NpgsqlProvider)
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE IF EXISTS "Events" ADD COLUMN IF NOT EXISTS "ExternalImageUrl" character varying(1000);
                ALTER TABLE IF EXISTS "Events" ADD COLUMN IF NOT EXISTS "ImageBlobName" character varying(300);
                """);
            return;
        }

        if (provider != SqliteProvider)
            return;

        var existingColumns = await GetSqliteColumnNamesAsync(db, "Events");
        if (existingColumns.Count == 0)
            return;
        if (existingColumns.Contains("ExternalImageUrl") && existingColumns.Contains("ImageBlobName"))
            return;

        if (!existingColumns.Contains("ExternalImageUrl"))
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Events" ADD COLUMN "ExternalImageUrl" TEXT NULL;""");
        if (!existingColumns.Contains("ImageBlobName"))
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Events" ADD COLUMN "ImageBlobName" TEXT NULL;""");

        logger.LogInformation("Repaired missing event image columns before running migrations.");
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
}
