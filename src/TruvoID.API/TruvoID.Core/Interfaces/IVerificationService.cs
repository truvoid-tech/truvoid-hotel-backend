using TruvoID.Core.DTOs;
using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

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
