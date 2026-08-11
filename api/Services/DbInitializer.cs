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
    public static async Task InitializeAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration skipped/failed at startup.");
            return;
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
                DisplayName = adminEmail
            };
            db.Users.Add(existing);
        }

        if (!existing.Roles.Any(r => r.Role == Roles.Admin))
        {
            existing.Roles.Add(new UserRole { Role = Roles.Admin, UserId = existing.Id });
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded admin role for {Email}", adminEmail);
        }
    }
}
