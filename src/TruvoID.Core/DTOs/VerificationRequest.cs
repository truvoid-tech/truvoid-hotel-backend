using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

public record VerificationRequest
{
    public VerificationType Type { get; init; }
    public string SubjectRef { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
}
