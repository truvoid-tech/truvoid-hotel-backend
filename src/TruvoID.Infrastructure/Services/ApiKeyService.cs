using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private const int KeyLength = 48; // 48 bytes = 64 hex chars after encoding
    private const string KeyPrefix = "tv_live_";

    private readonly TruvoIDDbContext _db;

    public ApiKeyService(TruvoIDDbContext db)
    {
        _db = db;
    }

    public async Task<ApiKeyResponse> GenerateKeyAsync(Guid institutionId, string? description = null, CancellationToken ct = default)
    {
        // Generate cryptographically secure random key
        var keyBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var rawKey = $"{KeyPrefix}{Convert.ToBase64String(keyBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
        var keyHash = ComputeHash(rawKey);
        var keyPrefix = rawKey[..16]; // e.g. "tv_live_AbCdEf"

        var apiKey = new ApiKey
        {
            InstitutionId = institutionId,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Description = description,
            Status = ApiKeyStatus.Active
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return new ApiKeyResponse
        {
            Id = apiKey.Id,
            KeyPrefix = keyPrefix,
            Description = description,
            Status = ApiKeyStatus.Active,
            CreatedAt = apiKey.CreatedAt,
            RawKey = rawKey // Only returned on creation
        };
    }

    public async Task<bool> RevokeKeyAsync(Guid institutionId, Guid keyId, CancellationToken ct = default)
    {
        var key = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.InstitutionId == institutionId, ct);

        if (key is null || key.Status == ApiKeyStatus.Revoked)
            return false;

        key.Status = ApiKeyStatus.Revoked;
        key.RevokedAt = DateTime.UtcNow;
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ApiKey?> ValidateKeyAsync(string rawKey, CancellationToken ct = default)
    {
        var keyHash = ComputeHash(rawKey);

        return await _db.ApiKeys
            .Include(k => k.Institution)
            .FirstOrDefaultAsync(
                k => k.KeyHash == keyHash && k.Status == ApiKeyStatus.Active,
                ct);
    }

    public async Task<List<ApiKeyResponse>> GetKeysAsync(Guid institutionId, CancellationToken ct = default)
    {
        return await _db.ApiKeys
            .Where(k => k.InstitutionId == institutionId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyResponse
            {
                Id = k.Id,
                KeyPrefix = k.KeyPrefix,
                Description = k.Description,
                Status = k.Status,
                CreatedAt = k.CreatedAt,
                RevokedAt = k.RevokedAt
            })
            .ToListAsync(ct);
    }

    private static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
