namespace BulletinBoard.Api.Data.Entities;

/// <summary>
/// Accountability trail for moderation, role changes and GDPR actions.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>e.g. "event.approve", "event.reject", "role.grant", "gdpr.export", "gdpr.erase".</summary>
    public string Action { get; set; } = default!;

    public Guid? ActorUserId { get; set; }
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    /// <summary>Free-form JSON detail (reason, granted role, ...). No sensitive PII.</summary>
    public string? Detail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
