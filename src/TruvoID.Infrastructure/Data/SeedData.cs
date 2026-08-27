using Microsoft.EntityFrameworkCore;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;

namespace TruvoID.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(TruvoIDDbContext db)
    {
        // Only seed if the database is empty
        if (await db.PricingRates.AnyAsync())
            return;

        // Default pricing rates (NGN) — configurable via admin panel later
        var defaultRates = new List<PricingRate>
        {
            new()
            {
                Type = VerificationType.Nin,
                PricePerCall = 100.00m,
                NimcPartnerCost = 70.00m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            },
            new()
            {
                Type = VerificationType.Bvn,
                PricePerCall = 150.00m,
                NimcPartnerCost = 100.00m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            },
            new()
            {
                Type = VerificationType.Phone,
                PricePerCall = 50.00m,
                NimcPartnerCost = 30.00m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            }
        };

        db.PricingRates.AddRange(defaultRates);
        await db.SaveChangesAsync();
    }
}
