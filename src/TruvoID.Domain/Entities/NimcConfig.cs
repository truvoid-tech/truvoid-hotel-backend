using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// NIMC partner API configuration per environment (Live / Sandbox).
/// Platform admins manage these; verification service reads the active one.
/// </summary>
public class NimcConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Environment { get; set; } = "sandbox"; // "live" or "sandbox"
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? PartnerId { get; set; }
    public string? SecretKey { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
