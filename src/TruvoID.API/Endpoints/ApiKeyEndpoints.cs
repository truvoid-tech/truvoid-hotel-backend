using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Endpoints;

public static class ApiKeyEndpoints
{
    // The full key is only ever shown once, at creation. Storage keeps only a
    // SHA-256 hash (for auth lookups) and a short prefix (for display in the UI).
    private const string KeyPrefixTag = "trv_live_";

    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/api-keys").RequireAuthorization();

        group.MapGet("/", ListKeys);
        group.MapPost("/", CreateKey);
        group.MapDelete("/{id:guid}", RevokeKey);

        return app;
    }

    private static async Task<IResult> ListKeys(HttpContext ctx, MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var keys = await db.ApiKeys
            .Find(k => k.InstitutionId == institutionId)
            .SortByDescending(k => k.CreatedAt)
            .ToListAsync();

        return Results.Ok(keys.Select(k => MapResponse(k, rawKey: null)).ToList());
    }

    private static async Task<IResult> CreateKey(
        HttpContext ctx,
        CreateApiKeyRequest request,
        MongoDbContext db,
        IAuditService audit)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var rawKey = $"{KeyPrefixTag}{secret}";
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();

        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            KeyPrefix = rawKey[..(KeyPrefixTag.Length + 8)],
            KeyHash = keyHash,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.ApiKeys.InsertOneAsync(key);
        await audit.LogAsync(AuditAction.ApiKeyGenerated, nameof(ApiKey), key.Id, ctx.GetUserId(), "User", key.Description);

        return Results.Ok(MapResponse(key, rawKey));
    }

    private static async Task<IResult> RevokeKey(
        HttpContext ctx,
        Guid id,
        MongoDbContext db,
        IAuditService audit)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var update = Builders<ApiKey>.Update
            .Set(k => k.Status, "Revoked")
            .Set(k => k.RevokedAt, DateTime.UtcNow);

        // Scoped to InstitutionId too, so one institution can't revoke another's key by guessing an id.
        var result = await db.ApiKeys.UpdateOneAsync(
            k => k.Id == id && k.InstitutionId == institutionId, update);

        if (result.MatchedCount == 0)
            return Results.NotFound(new { error = "API key not found." });

        await audit.LogAsync(AuditAction.ApiKeyRevoked, nameof(ApiKey), id, ctx.GetUserId(), "User");

        return Results.NoContent();
    }

    private static ApiKeyResponse MapResponse(ApiKey key, string? rawKey) => new()
    {
        Id = key.Id.ToString(),
        KeyPrefix = key.KeyPrefix,
        Description = key.Description,
        Status = key.Status == "Active" ? 0 : 1, // ApiKeyStatus enum on the frontend: Active = 0, Revoked = 1
        CreatedAt = key.CreatedAt,
        RawKey = rawKey
    };

    public record CreateApiKeyRequest
    {
        public string Description { get; init; } = "";
    }
}

public class ApiKeyResponse
{
    public string Id { get; init; } = "";
    public string KeyPrefix { get; init; } = "";
    public string? Description { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? RawKey { get; init; }
}
