using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

public record CallHistoryResponse
{
    public Guid Id { get; init; }
    public VerificationType Type { get; init; }
    public VerificationStatus Status { get; init; }
    public decimal AmountCharged { get; init; }
    public string? ErrorMessage { get; init; }
    public string? IdempotencyKey { get; init; }
    public Guid? UserId { get; init; }
    public Guid? ApiKeyId { get; init; }
    public DateTime CreatedAt { get; init; }
}
