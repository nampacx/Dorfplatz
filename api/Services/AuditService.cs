using System.Text.Json;
using BulletinBoard.Api.Data;
using BulletinBoard.Api.Data.Entities;

namespace BulletinBoard.Api.Services;

/// <summary>Writes accountability records for moderation, role and GDPR actions.</summary>
public class AuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public void Record(string action, Guid? actorUserId, string? targetType = null,
        Guid? targetId = null, object? detail = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            ActorUserId = actorUserId,
            TargetType = targetType,
            TargetId = targetId,
            Detail = detail is null ? null : JsonSerializer.Serialize(detail)
        });
    }
}
