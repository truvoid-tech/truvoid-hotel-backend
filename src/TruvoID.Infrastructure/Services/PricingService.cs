using MongoDB.Driver;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class PricingService : IPricingService
{
    private readonly MongoDbContext _db;

    public PricingService(MongoDbContext db) => _db = db;

    public async Task<decimal> GetPriceAsync(VerificationType type, Guid? institutionId = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Try institution-specific rate first
        if (institutionId.HasValue)
        {
            var instRate = await _db.PricingRates.Find(
                r => r.Type == type
                     && r.InstitutionId == institutionId.Value
                     && r.IsActive
                     && r.EffectiveFrom <= now
                     && (r.EffectiveTo == null || r.EffectiveTo > now))
                .FirstOrDefaultAsync(ct);

            if (instRate is not null)
                return instRate.PricePerCall;
        }

        // Fall back to global default rate
        var globalRate = await _db.PricingRates.Find(
            r => r.Type == type
                 && r.InstitutionId == null
                 && r.IsActive
                 && r.EffectiveFrom <= now
                 && (r.EffectiveTo == null || r.EffectiveTo > now))
            .FirstOrDefaultAsync(ct);

        return globalRate?.PricePerCall ?? throw new InvalidOperationException($"No active pricing rate found for verification type {type}.");
    }
}
