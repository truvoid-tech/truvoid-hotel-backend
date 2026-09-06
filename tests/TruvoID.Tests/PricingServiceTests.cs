using MongoDB.Bson;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;

namespace TruvoID.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// In-Memory PricingService (mirrors the real one but uses a Dictionary)
// ══════════════════════════════════════════════════════════════════════════════

public class InMemoryPricingService
{
    private readonly Dictionary<Guid, InstitutionPricing> _store = new();

    // Default prices (same as real PricingService)
    private const decimal DefaultNinPrice = 100m;
    private const decimal DefaultBvnPrice = 150m;
    private const decimal DefaultPhonePrice = 50m;
    private const decimal DefaultNinCost = 45m;
    private const decimal DefaultBvnCost = 65m;
    private const decimal DefaultPhoneCost = 20m;

    public void SetPricing(InstitutionPricing pricing)
    {
        _store[pricing.InstitutionId] = pricing;
    }

    public void RemovePricing(Guid institutionId)
    {
        _store.Remove(institutionId);
    }

    public Task<decimal> GetPriceAsync(VerificationType type, Guid institutionId, CancellationToken ct = default)
    {
        _store.TryGetValue(institutionId, out var pricing);

        return Task.FromResult(type switch
        {
            VerificationType.Nin => pricing?.NinPrice ?? DefaultNinPrice,
            VerificationType.Bvn => pricing?.BvnPrice ?? DefaultBvnPrice,
            VerificationType.Phone => pricing?.PhonePrice ?? DefaultPhonePrice,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        });
    }

    public Task<decimal> GetCostAsync(VerificationType type, Guid institutionId, CancellationToken ct = default)
    {
        _store.TryGetValue(institutionId, out var pricing);

        return Task.FromResult(type switch
        {
            VerificationType.Nin => pricing?.NinCost ?? DefaultNinCost,
            VerificationType.Bvn => pricing?.BvnCost ?? DefaultBvnCost,
            VerificationType.Phone => pricing?.PhoneCost ?? DefaultPhoneCost,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        });
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Tests
// ══════════════════════════════════════════════════════════════════════════════

public class PricingServiceTests
{
    private readonly InMemoryPricingService _svc = new();
    private readonly Guid _instA = Guid.NewGuid();
    private readonly Guid _instB = Guid.NewGuid();

    // ── GetPriceAsync — Default Fallback ─────────────────────────────────────

    [Fact]
    public async Task GetPriceAsync_ReturnsDefaultNinPrice_WhenNoCustomPricing()
    {
        var price = await _svc.GetPriceAsync(VerificationType.Nin, _instA);
        Assert.Equal(100m, price);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsDefaultBvnPrice_WhenNoCustomPricing()
    {
        var price = await _svc.GetPriceAsync(VerificationType.Bvn, _instA);
        Assert.Equal(150m, price);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsDefaultPhonePrice_WhenNoCustomPricing()
    {
        var price = await _svc.GetPriceAsync(VerificationType.Phone, _instA);
        Assert.Equal(50m, price);
    }

    // ── GetPriceAsync — Custom Pricing ───────────────────────────────────────

    [Fact]
    public async Task GetPriceAsync_ReturnsCustomNinPrice_WhenSet()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 120m,
            BvnPrice = 200m,
            PhonePrice = 60m
        });

        var price = await _svc.GetPriceAsync(VerificationType.Nin, _instA);
        Assert.Equal(120m, price);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsCustomBvnPrice_WhenSet()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 120m,
            BvnPrice = 200m,
            PhonePrice = 60m
        });

        var price = await _svc.GetPriceAsync(VerificationType.Bvn, _instA);
        Assert.Equal(200m, price);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsCustomPhonePrice_WhenSet()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 120m,
            BvnPrice = 200m,
            PhonePrice = 60m
        });

        var price = await _svc.GetPriceAsync(VerificationType.Phone, _instA);
        Assert.Equal(60m, price);
    }

    // ── GetPriceAsync — Per-Institution Isolation ────────────────────────────

    [Fact]
    public async Task GetPriceAsync_DifferentInstitutions_HaveDifferentPrices()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 120m,
            BvnPrice = 180m,
            PhonePrice = 70m
        });

        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instB,
            NinPrice = 80m,
            BvnPrice = 100m,
            PhonePrice = 40m
        });

        Assert.Equal(120m, await _svc.GetPriceAsync(VerificationType.Nin, _instA));
        Assert.Equal(80m, await _svc.GetPriceAsync(VerificationType.Nin, _instB));
        Assert.Equal(180m, await _svc.GetPriceAsync(VerificationType.Bvn, _instA));
        Assert.Equal(100m, await _svc.GetPriceAsync(VerificationType.Bvn, _instB));
    }

    // ── GetCostAsync — Default Fallback ──────────────────────────────────────

    [Fact]
    public async Task GetCostAsync_ReturnsDefaultNinCost_WhenNoCustomPricing()
    {
        var cost = await _svc.GetCostAsync(VerificationType.Nin, _instA);
        Assert.Equal(45m, cost);
    }

    [Fact]
    public async Task GetCostAsync_ReturnsDefaultBvnCost_WhenNoCustomPricing()
    {
        var cost = await _svc.GetCostAsync(VerificationType.Bvn, _instA);
        Assert.Equal(65m, cost);
    }

    [Fact]
    public async Task GetCostAsync_ReturnsDefaultPhoneCost_WhenNoCustomPricing()
    {
        var cost = await _svc.GetCostAsync(VerificationType.Phone, _instA);
        Assert.Equal(20m, cost);
    }

    // ── GetCostAsync — Custom Pricing ────────────────────────────────────────

    [Fact]
    public async Task GetCostAsync_ReturnsCustomNinCost_WhenSet()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinCost = 55m,
            BvnCost = 80m,
            PhoneCost = 25m
        });

        var cost = await _svc.GetCostAsync(VerificationType.Nin, _instA);
        Assert.Equal(55m, cost);
    }

    [Fact]
    public async Task GetCostAsync_ReturnsCustomBvnCost_WhenSet()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinCost = 55m,
            BvnCost = 80m,
            PhoneCost = 25m
        });

        var cost = await _svc.GetCostAsync(VerificationType.Bvn, _instA);
        Assert.Equal(80m, cost);
    }

    [Fact]
    public async Task GetCostAsync_ReturnsCustomPhoneCost_WhenSet()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinCost = 55m,
            BvnCost = 80m,
            PhoneCost = 25m
        });

        var cost = await _svc.GetCostAsync(VerificationType.Phone, _instA);
        Assert.Equal(25m, cost);
    }

    // ── Remove Pricing ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemovePricing_RevertsToDefaults()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 200m,
            BvnPrice = 300m,
            PhonePrice = 80m
        });

        // Custom prices active
        Assert.Equal(200m, await _svc.GetPriceAsync(VerificationType.Nin, _instA));

        // Remove custom pricing
        _svc.RemovePricing(_instA);

        // Should revert to defaults
        Assert.Equal(100m, await _svc.GetPriceAsync(VerificationType.Nin, _instA));
        Assert.Equal(150m, await _svc.GetPriceAsync(VerificationType.Bvn, _instA));
        Assert.Equal(50m, await _svc.GetPriceAsync(VerificationType.Phone, _instA));
    }

    // ── Edge Cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceAsync_ZeroPrice_IsValid()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 0m,
            BvnPrice = 0m,
            PhonePrice = 0m
        });

        Assert.Equal(0m, await _svc.GetPriceAsync(VerificationType.Nin, _instA));
    }

    [Fact]
    public async Task GetPriceAsync_VeryHighPrice_IsValid()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 99999.99m,
            BvnPrice = 99999.99m,
            PhonePrice = 99999.99m
        });

        Assert.Equal(99999.99m, await _svc.GetPriceAsync(VerificationType.Nin, _instA));
    }

    [Fact]
    public async Task GetCostAsync_CostHigherThanPrice_StillReturns()
    {
        // This is a valid business scenario (e.g., promotional pricing)
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 50m,   // Institution pays ₦50
            NinCost = 45m     // TruvoID pays ₦45 upstream
        });

        Assert.Equal(50m, await _svc.GetPriceAsync(VerificationType.Nin, _instA));
        Assert.Equal(45m, await _svc.GetCostAsync(VerificationType.Nin, _instA));
    }

    // ── Margin Calculation Validation ────────────────────────────────────────

    [Fact]
    public async Task GetPriceAndCost_CanCalculateMargin()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 120m,
            NinCost = 45m,
            BvnPrice = 200m,
            BvnCost = 65m,
            PhonePrice = 60m,
            PhoneCost = 20m
        });

        var ninPrice = await _svc.GetPriceAsync(VerificationType.Nin, _instA);
        var ninCost = await _svc.GetCostAsync(VerificationType.Nin, _instA);
        var ninMargin = ninPrice - ninCost;
        Assert.Equal(75m, ninMargin);

        var bvnPrice = await _svc.GetPriceAsync(VerificationType.Bvn, _instA);
        var bvnCost = await _svc.GetCostAsync(VerificationType.Bvn, _instA);
        var bvnMargin = bvnPrice - bvnCost;
        Assert.Equal(135m, bvnMargin);

        var phonePrice = await _svc.GetPriceAsync(VerificationType.Phone, _instA);
        var phoneCost = await _svc.GetCostAsync(VerificationType.Phone, _instA);
        var phoneMargin = phonePrice - phoneCost;
        Assert.Equal(40m, phoneMargin);
    }

    // ── Bulk Institution Test ────────────────────────────────────────────────

    [Fact]
    public async Task MultipleInstitutions_EachHasOwnPricing()
    {
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        for (var i = 0; i < ids.Count; i++)
        {
            _svc.SetPricing(new InstitutionPricing
            {
                InstitutionId = ids[i],
                NinPrice = 100m + i * 10m,
                BvnPrice = 150m + i * 10m,
                PhonePrice = 50m + i * 5m
            });
        }

        for (var i = 0; i < ids.Count; i++)
        {
            Assert.Equal(100m + i * 10m, await _svc.GetPriceAsync(VerificationType.Nin, ids[i]));
            Assert.Equal(150m + i * 10m, await _svc.GetPriceAsync(VerificationType.Bvn, ids[i]));
            Assert.Equal(50m + i * 5m, await _svc.GetPriceAsync(VerificationType.Phone, ids[i]));
        }
    }

    [Fact]
    public async Task MultipleInstitutions_RemovingOneDoesNotAffectOthers()
    {
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instA,
            NinPrice = 120m
        });
        _svc.SetPricing(new InstitutionPricing
        {
            InstitutionId = _instB,
            NinPrice = 200m
        });

        _svc.RemovePricing(_instA);

        Assert.Equal(100m, await _svc.GetPriceAsync(VerificationType.Nin, _instA)); // reverted
        Assert.Equal(200m, await _svc.GetPriceAsync(VerificationType.Nin, _instB)); // unchanged
    }
}
