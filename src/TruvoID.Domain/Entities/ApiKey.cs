using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Per-institution API key. Never stores the raw key — only the SHA-256 hash.
/// Scoped, revocable, with optional rate-limit configuration.
/// </summary>
public class ApiKey : BaseEntity
{
    public Guid InstitutionId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty; // First 8 chars for identification (e.g. "tv_live_")
    public string? Description { get; set; }
    public ApiKeyStatus Status { get; set; } = ApiKeyStatus.Active;
    public DateTime? RevokedAt { get; set; }
    
    // Rate limiting (per-key, configurable per institution tier)
    public int? RateLimitPerMinute { get; set; }
    public int? RateLimitPerDay { get; set; }
    
    // Navigation properties
    public Institution Institution { get; set; } = null!;
    public ICollection<VerificationCall> VerificationCalls { get; set; } = new List<VerificationCall>();
}
