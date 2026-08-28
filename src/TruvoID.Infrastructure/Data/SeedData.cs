using Microsoft.EntityFrameworkCore;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;

namespace TruvoID.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(TruvoIDDbContext db)
    {
        // ─── PlatformAdmin user ───
        // Only seed if no PlatformAdmin exists
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.PlatformAdmin))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@truvoid.ng",
                FullName = "TruvoID Platform Admin",
                PhoneNumber = "+2348000000000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                Role = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                DailyCallLimit = null,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            Console.WriteLine("[Seed] PlatformAdmin created: admin@truvoid.ng / Admin@12345");
        }

        // ─── Default pricing rates ───
        if (await db.PricingRates.AnyAsync())
            return;

        var defaultRates = new List<PricingRate>
        {
            new()
            {
                Type = VerificationType.Nin,
                PricePerCall = 100.00m,
                NimcPartnerCost = 45.00m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            },
            new()
            {
                Type = VerificationType.Bvn,
                PricePerCall = 150.00m,
                NimcPartnerCost = 65.00m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            },
            new()
            {
                Type = VerificationType.Phone,
                PricePerCall = 50.00m,
                NimcPartnerCost = 20.00m,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            }
        };

        db.PricingRates.AddRange(defaultRates);
        await db.SaveChangesAsync();

        Console.WriteLine("[Seed] Default pricing rates created.");
    }
}
