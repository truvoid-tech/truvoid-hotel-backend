using TruvoID.Core.DTOs;
using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// Verification service — orchestrates the full verification call flow:
/// check wallet → reserve debit → call NIMC partner → commit/reverse → return result.
/// </summary>
public interface IVerificationService
{
    Task<VerificationResponse> VerifyAsync(
        Guid institutionId,
        VerificationType type,
        string subjectRef,
        Guid? userId = null,
        Guid? apiKeyId = null,
        string? idempotencyKey = null,
        CancellationToken ct = default);
}
