using BulletinBoard.Api.Data;
using BulletinBoard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BulletinBoard.Api.Tests.Services;

public sealed class DbInitializerTests
{
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
}
