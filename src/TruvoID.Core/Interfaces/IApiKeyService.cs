using TruvoID.Core.DTOs;
using TruvoID.Domain.Entities;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// API key management service.
/// Handles generation (with raw key returned once), validation, rotation, and revocation.
/// </summary>
public interface IApiKeyService
{
    Task<ApiKeyResponse> GenerateKeyAsync(Guid institutionId, string? description = null, CancellationToken ct = default);
    Task<bool> RevokeKeyAsync(Guid institutionId, Guid keyId, CancellationToken ct = default);
    Task<ApiKey?> ValidateKeyAsync(string rawKey, CancellationToken ct = default);
    Task<List<ApiKeyResponse>> GetKeysAsync(Guid institutionId, CancellationToken ct = default);
}
