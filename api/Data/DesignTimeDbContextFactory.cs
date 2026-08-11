using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BulletinBoard.Api.Data;

/// <summary>
/// Enables `dotnet ef migrations` at design time (CI generates the migration bundle).
/// Reads the connection string from Postgres__ConnectionString or a design-time default.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("Postgres__ConnectionString")
                   ?? "Host=localhost;Port=5432;Database=bulletinboard;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AppDbContext(options);
    }
}
