namespace BulletinBoard.Api.Data.Entities;

/// <summary>
/// An event / news submission (party, Vereins-Event). Moderated before it goes public.
/// </summary>
public class EventPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTimeOffset? EventDate { get; set; }
    public string? Location { get; set; }

    /// <summary>Existing rows predate categories, so they fall back to Sonstiges.</summary>
    public EventCategory Category { get; set; } = EventCategory.Sonstiges;

    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = default!;

    public EventStatus Status { get; set; } = EventStatus.Pending;

    // Moderation outcome
    public string? RejectionReason { get; set; }
    public Guid? ModeratedById { get; set; }
    public DateTimeOffset? ModeratedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
