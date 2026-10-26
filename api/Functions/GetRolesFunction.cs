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

    public class ClaimPayload
    {
        [JsonPropertyName("typ")] public string? Typ { get; set; }
        [JsonPropertyName("val")] public string? Val { get; set; }
    }

    public class RolesPayload
    {
        [JsonPropertyName("identityProvider")] public string? IdentityProvider { get; set; }
        [JsonPropertyName("userId")] public string? UserId { get; set; }
        [JsonPropertyName("userDetails")] public string? UserDetails { get; set; }

        /// <summary>
        /// The provider's claims. This is the only place they are available:
        /// the x-ms-client-principal header the other functions receive does
        /// not carry a claims array, so a real name can be captured here or
        /// nowhere.
        /// </summary>
        [JsonPropertyName("claims")] public List<ClaimPayload>? Claims { get; set; }
    }

    private const string SchemaName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
    private const string SchemaGivenName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname";
    private const string SchemaSurname = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname";

    /// <summary>
    /// The person's actual name, or null if the provider did not send one.
    ///
    /// Entra sends two claims called "name": the short one holds "Ellen
    /// Contoso", the schema-suffixed one holds "ellen@contoso.com". Taking
    /// whichever appears first would put email addresses back on the board,
    /// so the short form is preferred and anything containing an @ is
    /// rejected as an address rather than a name.
    /// </summary>
    private static string? ResolveDisplayName(RolesPayload payload)
    {
        string? Claim(string type) => payload.Claims?
            .FirstOrDefault(c => string.Equals(c.Typ, type, StringComparison.OrdinalIgnoreCase)
                                 && !string.IsNullOrWhiteSpace(c.Val))?.Val?.Trim();

        var name = Claim("name") ?? Claim(SchemaName);
        if (!string.IsNullOrWhiteSpace(name) && !name.Contains('@')) return name;

        // Google and Entra both offer the parts even when the full name is absent.
        var given = Claim("given_name") ?? Claim(SchemaGivenName);
        var family = Claim("family_name") ?? Claim(SchemaSurname);
        var joined = string.Join(' ', new[] { given, family }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
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
            // Reuse an existing account when the identity provider assigns a new user id.
            // Prefer a real account over a seeded one, then choose the oldest duplicate.
            var emailMatches = await _db.Users
                .Include(u => u.Roles)
                .Where(u => u.Email != null && u.Email.ToLower() == payload.UserDetails.ToLower())
                .ToListAsync();
            user = emailMatches
                .OrderBy(u => u.ProviderUserId.StartsWith("seed:") ? 1 : 0)
                .ThenBy(u => u.CreatedAt)
                .ThenBy(u => u.Id)
                .FirstOrDefault();
            if (user is not null)
                user.ProviderUserId = payload.UserId;
        }

        var providerName = ResolveDisplayName(payload);

        if (user is null)
        {
            user = new AppUser
            {
                ProviderUserId = payload.UserId,
                Email = payload.UserDetails,
                // Falls back to the address only when the provider sent no name.
                DisplayName = providerName ?? payload.UserDetails
            };
            _db.Users.Add(user);
        }
        else if (user.DeletedAt is not null)
        {
            // Erased accounts get no elevated roles.
            return new OkObjectResult(new { roles = Array.Empty<string>() });
        }
        else if (providerName is not null &&
                 (string.IsNullOrWhiteSpace(user.DisplayName) || user.DisplayName == user.Email))
        {
            // Accounts created before names were captured show an email address
            // next to their posts. Upgrade them on the next sign-in — but only
            // while the stored name is still just the address, so a name the
            // member chose on the account page is never overwritten.
            user.DisplayName = providerName;
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

        // Whether a name arrived, not what it was: names are personal data and
        // this log is not the place for them.
        _logger.LogInformation("Assigned roles {Roles} to {UserId} (provider name: {HasName})",
            string.Join(",", roles), payload.UserId, providerName is not null);
        return new OkObjectResult(new { roles });
    }
}
