using TruvoID.Core.DTOs;

namespace TruvoID.Core.Interfaces;

public interface INimcConfigService
{
    Task<NimcEnvironmentDto> GetActiveEnvironmentAsync(CancellationToken ct = default);
    Task SetActiveEnvironmentAsync(string environment, CancellationToken ct = default);
    string GetApiBaseUrl();
    string GetApiKey();
}
