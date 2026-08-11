using System.Text;
using System.Text.Json;
using BulletinBoard.Api.Auth;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BulletinBoard.Api.Services;

/// <summary>
/// Reads the SWA client principal from the request and maps it to an <see cref="AppUser"/>.
/// The AppUser is upserted on first sight so authoring/relations always have a row.
/// Role checks are server-side and never trust the client.
/// </summary>
public class CurrentUserService
{
    private readonly AppDbContext _db;

    public CurrentUserService(AppDbContext db) => _db = db;

    public ClientPrincipal? ReadPrincipal(HttpRequest req)
    {
        if (!req.Headers.TryGetValue("x-ms-client-principal", out var header) || header.Count == 0)
            return null;

        try
        {
            var decoded = Convert.FromBase64String(header.ToString());
            var json = Encoding.UTF8.GetString(decoded);
            return JsonSerializer.Deserialize<ClientPrincipal>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns the persisted AppUser for the caller, creating it if needed. Null if anonymous.</summary>
    public async Task<AppUser?> GetOrCreateUserAsync(HttpRequest req, CancellationToken ct = default)
    {
        var principal = ReadPrincipal(req);
        if (principal?.UserId is null) return null;

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.ProviderUserId == principal.UserId, ct);

        if (user is null)
        {
            user = new AppUser
            {
                ProviderUserId = principal.UserId,
                Email = principal.UserDetails,
                DisplayName = principal.UserDetails
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }

        return user.DeletedAt is null ? user : null;
    }

    /// <summary>Effective roles for a user: always "user", plus any granted moderator/admin rows.</summary>
    public static IReadOnlyCollection<string> EffectiveRoles(AppUser user)
    {
        var roles = new HashSet<string> { Roles.User };
        foreach (var r in user.Roles) roles.Add(r.Role);
        return roles;
    }

    public static bool IsInRole(AppUser user, string role) => EffectiveRoles(user).Contains(role);

    public static bool IsModerator(AppUser user) =>
        IsInRole(user, Roles.Moderator) || IsInRole(user, Roles.Admin);

    public static bool IsAdmin(AppUser user) => IsInRole(user, Roles.Admin);
}
