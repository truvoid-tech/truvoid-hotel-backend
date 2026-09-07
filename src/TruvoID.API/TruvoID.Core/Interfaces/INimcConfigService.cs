using TruvoID.Domain.Entities;

namespace TruvoID.Core.Interfaces;

public interface INimcConfigService
{
    Task<NimcConfig?> GetByEnvironmentAsync(string environment);
    Task<List<NimcConfig>> GetAllAsync();
    Task<NimcConfig?> GetActiveAsync();
    Task UpsertAsync(string environment, string? apiKey);
    Task ActivateAsync(string environment);
    string GetApiKeyForEnvironment(string environment);
}
