using MongoDB.Bson.Serialization.Attributes;
using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

[BsonIgnoreExtraElements]
public class AuditLogEntry
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid? ActorId { get; set; }
    public string? ActorType { get; set; } // "User", "ApiKey"
    public AuditAction Action { get; set; }
    public string Entity { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
