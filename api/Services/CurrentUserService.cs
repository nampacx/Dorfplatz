using System.Text;
using System.Text.Json;
using BulletinBoard.Api.Auth;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            if (!string.IsNullOrWhiteSpace(principal.UserDetails))
            {
                var emailMatches = await _db.Users
                    .Include(u => u.Roles)
                    .Where(u => u.Email != null && u.Email.ToLower() == principal.UserDetails.ToLower())
                    .ToListAsync(ct);
                user = emailMatches
                    .OrderBy(u => u.ProviderUserId.StartsWith("seed:") ? 1 : 0)
                    .ThenBy(u => u.CreatedAt)
                    .ThenBy(u => u.Id)
                    .FirstOrDefault();
            }

            if (user is null)
            {
                user = new AppUser
                {
                    ProviderUserId = principal.UserId,
                    Email = principal.UserDetails,
                    DisplayName = principal.UserDetails
                };
                _db.Users.Add(user);
            }
            else
            {
                user.ProviderUserId = principal.UserId;
            }

            await _db.SaveChangesAsync(ct);
        }

        return user.DeletedAt is null ? user : null;
    }

    /// <summary>
    /// Signed in and let onto the board by an admin. Signing in creates an
    /// account; it does not grant access.
    /// </summary>
    public static bool IsApproved(AppUser user) => user.Status == AccessStatus.Approved;

    // Endpoints guard access with the pair
    //
    //     if (me is null) return new UnauthorizedResult();
    //     if (!CurrentUserService.IsApproved(me)) return HttpResults.PendingApproval();
    //
    // rather than a combined helper. Signed-out and awaiting-approval need
    // different responses — 401 sends a visitor to sign in, 403 tells a member
    // their access is still being reviewed — and writing both checks out keeps
    // `me` provably non-null afterwards, which a helper returning a nullable
    // tuple cannot do without a null-forgiving operator at all 26 call sites.
    //
    // Endpoints a waiting member must still reach — account/me, consent, data
    // export, erasure — deliberately omit the second check, or the waiting
    // screen would have nothing to render and no way out of it.

    /// <summary>
    /// Roles this user has been granted: always "user", plus moderator/admin
    /// rows. Deliberately says nothing about approval — the admin table shows
    /// this, and it should report what was granted, not what is currently
    /// usable. Approval is enforced at the gates instead.
    /// </summary>
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
