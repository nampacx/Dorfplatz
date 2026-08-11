namespace BulletinBoard.Api.Data.Entities;

/// <summary>
/// A services / Dienstleistungen post: either "ich suche" (Seeking) or "ich biete" (Offering).
/// Not moderated - visible immediately.
/// </summary>
public class ServicePost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ServiceType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? Category { get; set; }

    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = default!;

    public ServiceStatus Status { get; set; } = ServiceStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
