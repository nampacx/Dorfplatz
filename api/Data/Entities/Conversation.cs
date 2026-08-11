namespace BulletinBoard.Api.Data.Entities;

/// <summary>
/// A one-to-one chat between two members, optionally anchored to a listing
/// (goods or service) so a reply keeps its context.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>"goods" | "service" | null (freeform).</summary>
    public string? ListingType { get; set; }
    public Guid? ListingId { get; set; }

    public Guid ParticipantAId { get; set; }
    public AppUser ParticipantA { get; set; } = default!;

    public Guid ParticipantBId { get; set; }
    public AppUser ParticipantB { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = default!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = default!;

    public string Body { get; set; } = default!;

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>Content is cleared (tombstoned) when a sender is erased under GDPR.</summary>
    public bool IsDeleted { get; set; }
}
