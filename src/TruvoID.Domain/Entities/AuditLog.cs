using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Immutable audit log for NDPR/DPCO compliance.
/// Records every verification call and wallet transaction.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? ActorId { get; set; } // User or API key that performed the action
    public string ActorType { get; set; } = string.Empty; // "User" or "ApiKey"
    public AuditAction Action { get; set; }
    public string Entity { get; set; } = string.Empty; // Entity type name
    public Guid EntityId { get; set; }
    public string? DetailsJson { get; set; } // Optional JSON details about the action
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
