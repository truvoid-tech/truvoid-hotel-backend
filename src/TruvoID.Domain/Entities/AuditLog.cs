using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? ActorId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string Entity { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
