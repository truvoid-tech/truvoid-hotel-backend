using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// Pricing service for looking up per-call rates.
/// Supports institution-specific overrides and global defaults.
/// </summary>
public interface IPricingService
{
    Task<decimal> GetPriceAsync(VerificationType type, Guid? institutionId = null, CancellationToken ct = default);
}
