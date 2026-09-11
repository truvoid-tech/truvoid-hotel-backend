using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Endpoints;

public static class CallHistoryEndpoints
{
    public static IEndpointRouteBuilder MapCallHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/calls")
            .RequireAuthorization();

        group.MapGet("/", GetCalls);

        return app;
    }

    private static async Task<IResult> GetCalls(
        HttpContext ctx,
        [AsParameters] CallHistoryQuery query,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var filterBuilder = Builders<VerificationCall>.Filter;
        var filter = filterBuilder.Eq(c => c.InstitutionId, institutionId);

        if (query.Type is not null)
            filter &= filterBuilder.Eq(c => c.Type, query.Type.Value);
        if (query.Status is not null)
            filter &= filterBuilder.Eq(c => c.Status, query.Status.Value);

        var docs = await db.VerificationCalls
            .Find(filter)
            .SortByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync();

        var total = await db.VerificationCalls.CountDocumentsAsync(filter);

        return Results.Ok(new PaginatedResponse<CallHistoryResponse>
        {
            Items = docs.Select(MapCall).ToList(),
            TotalCount = (int)total,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    private static CallHistoryResponse MapCall(VerificationCall c) => new()
    {
        Id = c.Id.ToString(),
        IdempotencyKey = c.IdempotencyKey,
        ApiKeyId = c.ApiKeyId?.ToString(),
        Type = c.Type,
        Status = c.Status,
        Cost = c.AmountCharged,
        CreatedAt = c.CreatedAt
    };

    public record CallHistoryQuery
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public VerificationType? Type { get; init; }
        public VerificationStatus? Status { get; init; }
    }
}

public class CallHistoryResponse
{
    public string Id { get; init; } = "";
    public string? IdempotencyKey { get; init; }
    public string? ApiKeyId { get; init; }
    public VerificationType Type { get; init; }
    public VerificationStatus Status { get; init; }
    public decimal Cost { get; init; }
    public DateTime CreatedAt { get; init; }
}
