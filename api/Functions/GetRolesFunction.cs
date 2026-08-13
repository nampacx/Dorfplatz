using System.Text.Json;
using System.Text.Json.Serialization;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BulletinBoard.Api.Functions;

/// <summary>
/// Called by Static Web Apps after each successful login (configured via
/// <c>auth.rolesSource = "/api/GetRoles"</c>). Upserts the user and returns
/// their effective custom roles. Everyone gets "user"; moderators/admins get extra.
/// Not reachable by external HTTP once wired as rolesSource.
/// </summary>
public class GetRolesFunction
{
    private readonly AppDbContext _db;
    private readonly ILogger<GetRolesFunction> _logger;

    public GetRolesFunction(AppDbContext db, ILogger<GetRolesFunction> logger)
    {
        _db = db;
        _logger = logger;
    }

    public class RolesPayload
    {
        [JsonPropertyName("identityProvider")] public string? IdentityProvider { get; set; }
        [JsonPropertyName("userId")] public string? UserId { get; set; }
        [JsonPropertyName("userDetails")] public string? UserDetails { get; set; }
    }

    [Function("GetRoles")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "GetRoles")] HttpRequest req)
    {
        var payload = await JsonSerializer.DeserializeAsync<RolesPayload>(req.Body);
        if (payload?.UserId is null)
            return new OkObjectResult(new { roles = Array.Empty<string>() });

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.ProviderUserId == payload.UserId);

        if (user is null && !string.IsNullOrWhiteSpace(payload.UserDetails))
        {
            // Adopt a pre-provisioned (seeded) account matched by email on first real login.
            var seeded = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == payload.UserDetails && u.ProviderUserId.StartsWith("seed:"));
            if (seeded is not null)
            {
                seeded.ProviderUserId = payload.UserId;
                user = seeded;
            }
        }

        if (user is null)
        {
            user = new AppUser
            {
                ProviderUserId = payload.UserId,
                Email = payload.UserDetails,
                DisplayName = payload.UserDetails
            };
            _db.Users.Add(user);
        }
        else if (user.DeletedAt is not null)
        {
            // Erased accounts get no elevated roles.
            return new OkObjectResult(new { roles = Array.Empty<string>() });
        }

        await _db.SaveChangesAsync();

        // Signing in creates the account; an admin decides whether it may be
        // used. Until then the person is "authenticated" to Static Web Apps but
        // holds no application role, so role-gated routes stay closed to them.
        if (user.Status != AccessStatus.Approved)
        {
            _logger.LogInformation("No roles for {UserId}: access is {Status}", payload.UserId, user.Status);
            return new OkObjectResult(new { roles = Array.Empty<string>() });
        }

        var roles = new HashSet<string> { Roles.User };
        foreach (var r in user.Roles) roles.Add(r.Role);

        _logger.LogInformation("Assigned roles {Roles} to {UserId}", string.Join(",", roles), payload.UserId);
        return new OkObjectResult(new { roles });
    }
}
