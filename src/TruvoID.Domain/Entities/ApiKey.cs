using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class ApiKey : BaseEntity
{
    public Guid InstitutionId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ApiKeyStatus Status { get; set; } = ApiKeyStatus.Active;
    public DateTime? RevokedAt { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public int? RateLimitPerDay { get; set; }
}
