namespace BulletinBoard.Api.Data.Entities;

/// <summary>
/// A member of the bulletin board. Keyed by the Static Web Apps principal userId.
/// Personal fields are nulled on GDPR erasure (soft delete via <see cref="DeletedAt"/>).
/// </summary>
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The SWA principal <c>userId</c> (stable per provider identity).</summary>
    public string ProviderUserId { get; set; } = default!;

    public string? Email { get; set; }
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when the user accepted the privacy policy / consent.</summary>
    public DateTimeOffset? ConsentedAt { get; set; }

    /// <summary>Set on GDPR erasure. Personal fields are cleared and the account is hidden.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Whether an admin has let this person onto the board. Signing in creates
    /// the account; it does not grant access.
    /// </summary>
    public AccessStatus Status { get; set; } = AccessStatus.Pending;

    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    /// <summary>One of <see cref="Roles"/> (moderator / admin). The default "user" role is implicit.</summary>
    public string Role { get; set; } = default!;

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? GrantedByUserId { get; set; }
}
