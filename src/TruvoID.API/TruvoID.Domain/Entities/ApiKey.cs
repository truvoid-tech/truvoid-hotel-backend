using MongoDB.Bson.Serialization.Attributes;

namespace TruvoID.Domain.Entities;

[BsonIgnoreExtraElements]
public class ApiKey
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    /// <summary>Short, non-secret prefix shown in the UI (e.g. "trv_live_ab12cd34"). The full key is never stored.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the full raw key.</summary>
    public string KeyHash { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string Status { get; set; } = "Active"; // Active, Revoked
    public int CallCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
