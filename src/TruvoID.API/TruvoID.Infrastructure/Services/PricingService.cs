using MongoDB.Driver;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class PricingService : IPricingService
{
    private readonly MongoDbContext _db;

    // Global default prices (used when no per-institution record exists)
    private const decimal DefaultNinPrice = 100m;
    private const decimal DefaultBvnPrice = 150m;
    private const decimal DefaultPhonePrice = 50m;

    // Global default NIMC costs
    private const decimal DefaultNinCost = 45m;
    private const decimal DefaultBvnCost = 65m;
    private const decimal DefaultPhoneCost = 20m;

    public PricingService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetPriceAsync(VerificationType type, Guid institutionId, CancellationToken ct = default)
    {
        var pricing = await GetInstitutionPricingAsync(institutionId, ct);

        return type switch
        {
            VerificationType.Nin => pricing?.NinPrice ?? DefaultNinPrice,
            VerificationType.Bvn => pricing?.BvnPrice ?? DefaultBvnPrice,
            VerificationType.Phone => pricing?.PhonePrice ?? DefaultPhonePrice,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported verification type.")
        };
    }

    public async Task<decimal> GetCostAsync(VerificationType type, Guid institutionId, CancellationToken ct = default)
    {
        var pricing = await GetInstitutionPricingAsync(institutionId, ct);

        return type switch
        {
            VerificationType.Nin => pricing?.NinCost ?? DefaultNinCost,
            VerificationType.Bvn => pricing?.BvnCost ?? DefaultBvnCost,
            VerificationType.Phone => pricing?.PhoneCost ?? DefaultPhoneCost,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported verification type.")
        };
    }

    private async Task<InstitutionPricing?> GetInstitutionPricingAsync(Guid institutionId, CancellationToken ct)
    {
        return await _db.InstitutionPricings
            .Find(p => p.InstitutionId == institutionId)
            .FirstOrDefaultAsync(ct);
    }
}
