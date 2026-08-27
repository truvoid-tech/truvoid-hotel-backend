using Microsoft.EntityFrameworkCore;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class CallHistoryService : ICallHistoryService
{
    private readonly TruvoIDDbContext _db;

    public CallHistoryService(TruvoIDDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedResponse<CallHistoryResponse>> GetCallsAsync(
        Guid institutionId,
        int page = 1,
        int pageSize = 20,
        VerificationType? type = null,
        VerificationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var query = _db.VerificationCalls
            .Where(c => c.InstitutionId == institutionId);

        if (type.HasValue)
            query = query.Where(c => c.Type == type.Value);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(c => c.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(c => c.CreatedAt <= toDate.Value);

        if (userId.HasValue)
            query = query.Where(c => c.UserId == userId.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CallHistoryResponse
            {
                Id = c.Id,
                Type = c.Type,
                Status = c.Status,
                AmountCharged = c.AmountCharged,
                ErrorMessage = c.ErrorMessage,
                IdempotencyKey = c.IdempotencyKey,
                UserId = c.UserId,
                ApiKeyId = c.ApiKeyId,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        return new PaginatedResponse<CallHistoryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
