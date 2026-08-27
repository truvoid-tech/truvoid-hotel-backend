using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

public record ApiKeyResponse
{
    public Guid Id { get; init; }
    public string KeyPrefix { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ApiKeyStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    
    // Only populated on creation — raw API key (shown once)
    public string? RawKey { get; init; }
}
