using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class CallHistoryService : ICallHistoryService
{
    private readonly MongoDbContext _db;

    public CallHistoryService(MongoDbContext db) => _db = db;

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
        var filter = Builders<Domain.Entities.VerificationCall>.Filter.Eq(c => c.InstitutionId, institutionId);

        if (type.HasValue)
            filter = Builders<Domain.Entities.VerificationCall>.Filter.And(filter,
                Builders<Domain.Entities.VerificationCall>.Filter.Eq(c => c.Type, type.Value));

        if (status.HasValue)
            filter = Builders<Domain.Entities.VerificationCall>.Filter.And(filter,
                Builders<Domain.Entities.VerificationCall>.Filter.Eq(c => c.Status, status.Value));

        if (fromDate.HasValue)
            filter = Builders<Domain.Entities.VerificationCall>.Filter.And(filter,
                Builders<Domain.Entities.VerificationCall>.Filter.Gte(c => c.CreatedAt, fromDate.Value));

        if (toDate.HasValue)
            filter = Builders<Domain.Entities.VerificationCall>.Filter.And(filter,
                Builders<Domain.Entities.VerificationCall>.Filter.Lte(c => c.CreatedAt, toDate.Value));

        if (userId.HasValue)
            filter = Builders<Domain.Entities.VerificationCall>.Filter.And(filter,
                Builders<Domain.Entities.VerificationCall>.Filter.Eq(c => c.UserId, userId.Value));

        var totalCount = (int)await _db.VerificationCalls.CountDocumentsAsync(filter, cancellationToken: ct);

        var items = await _db.VerificationCalls
            .Find(filter)
            .SortByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return new PaginatedResponse<CallHistoryResponse>
        {
            Items = items.Select(c => new CallHistoryResponse
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
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
