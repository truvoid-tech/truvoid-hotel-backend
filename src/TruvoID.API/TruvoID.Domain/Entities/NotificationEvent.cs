using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TruvoID.Domain.Entities;

/// <summary>
/// In-app notification event. Each row is one notification shown to an institution.
/// </summary>
public class NotificationEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public Guid InstitutionId { get; set; }

    /// <summary>Notification category: "low_balance", "verification_result", "approval", "payment", "staff_invitation", etc.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Short title shown in the bell dropdown.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Longer body text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional deep-link the notification points to (e.g. "/dashboard/verify/new", "/wallet").</summary>
    public string? ActionUrl { get; set; }

    /// <summary>true once the user has seen/dismissed it.</summary>
    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
