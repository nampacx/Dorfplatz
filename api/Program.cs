using BulletinBoard.Api.Data;
using BulletinBoard.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    // ASP.NET Core integration → HTTP triggers use HttpRequest / IActionResult.
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();

        var connString = context.Configuration["Postgres:ConnectionString"]
            ?? throw new InvalidOperationException("Postgres:ConnectionString is not configured.");

        services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connString));

        services.AddScoped<CurrentUserService>();
        services.AddScoped<AuditService>();
        services.AddSingleton<BlobService>();
    })
    .Build();

// Apply migrations + seed bootstrap admin on cold start.
await DbInitializer.InitializeAsync(host.Services);

host.Run();
