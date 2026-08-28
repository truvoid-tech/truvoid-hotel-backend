using System.Security.Cryptography;
using System.Text;
using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private const int KeyLength = 48;
    private const string KeyPrefix = "tv_live_";

    private readonly MongoDbContext _db;

    public ApiKeyService(MongoDbContext db) => _db = db;

    public async Task<ApiKeyResponse> GenerateKeyAsync(Guid institutionId, string? description = null, CancellationToken ct = default)
    {
        var keyBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var rawKey = $"{KeyPrefix}{Convert.ToBase64String(keyBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
        var keyHash = ComputeHash(rawKey);
        var keyPrefix = rawKey[..16];

        var apiKey = new ApiKey
        {
            InstitutionId = institutionId,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Description = description,
            Status = ApiKeyStatus.Active
        };

        await _db.ApiKeys.InsertOneAsync(apiKey, cancellationToken: ct);

        return new ApiKeyResponse
        {
            Id = apiKey.Id,
            KeyPrefix = keyPrefix,
            Description = description,
            Status = ApiKeyStatus.Active,
            CreatedAt = apiKey.CreatedAt,
            RawKey = rawKey
        };
    }

    public async Task<bool> RevokeKeyAsync(Guid institutionId, Guid keyId, CancellationToken ct = default)
    {
        var key = await _db.ApiKeys.Find(k => k.Id == keyId && k.InstitutionId == institutionId).FirstOrDefaultAsync(ct);

        if (key is null || key.Status == ApiKeyStatus.Revoked)
            return false;

        var update = Builders<ApiKey>.Update
            .Set(k => k.Status, ApiKeyStatus.Revoked)
            .Set(k => k.RevokedAt, DateTime.UtcNow)
            .Set(k => k.UpdatedAt, DateTime.UtcNow);
        await _db.ApiKeys.UpdateOneAsync(k => k.Id == keyId, update, cancellationToken: ct);
        return true;
    }

    public async Task<ApiKey?> ValidateKeyAsync(string rawKey, CancellationToken ct = default)
    {
        var keyHash = ComputeHash(rawKey);
        return await _db.ApiKeys.Find(k => k.KeyHash == keyHash && k.Status == ApiKeyStatus.Active).FirstOrDefaultAsync(ct);
    }

    public async Task<List<ApiKeyResponse>> GetKeysAsync(Guid institutionId, CancellationToken ct = default)
    {
        var keys = await _db.ApiKeys
            .Find(k => k.InstitutionId == institutionId)
            .SortByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys.Select(k => new ApiKeyResponse
        {
            Id = k.Id,
            KeyPrefix = k.KeyPrefix,
            Description = k.Description,
            Status = k.Status,
            CreatedAt = k.CreatedAt,
            RevokedAt = k.RevokedAt
        }).ToList();
    }

    private static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
