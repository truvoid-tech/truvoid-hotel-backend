using TruvoID.Core.DTOs;

namespace TruvoID.Core.Interfaces;

public interface INimcConfigService
{
    Task<List<NimcConfigDto>> GetAllAsync(CancellationToken ct = default);
    Task<NimcConfigDto?> GetActiveAsync(CancellationToken ct = default);
    Task UpsertAsync(string environment, UpdateNimcConfigRequest request, CancellationToken ct = default);
    Task ActivateAsync(string environment, CancellationToken ct = default);
}
