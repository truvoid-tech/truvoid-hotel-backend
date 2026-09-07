using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

public interface IPricingService
{
    /// <summary>
    /// Get the price for a specific verification type for an institution.
    /// Falls back to global defaults if no custom pricing is configured.
    /// </summary>
    Task<decimal> GetPriceAsync(VerificationType type, Guid institutionId, CancellationToken ct = default);

    /// <summary>
    /// Get the NIMC cost (TruvoID's internal cost) for a verification type.
    /// </summary>
    Task<decimal> GetCostAsync(VerificationType type, Guid institutionId, CancellationToken ct = default);
}
