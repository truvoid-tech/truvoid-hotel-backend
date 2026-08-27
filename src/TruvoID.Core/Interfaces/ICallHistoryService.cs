using TruvoID.Core.DTOs;
using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// Service for querying paginated verification call history.
/// </summary>
public interface ICallHistoryService
{
    Task<PaginatedResponse<CallHistoryResponse>> GetCallsAsync(
        Guid institutionId,
        int page = 1,
        int pageSize = 20,
        VerificationType? type = null,
        VerificationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? userId = null,
        CancellationToken ct = default);
}
