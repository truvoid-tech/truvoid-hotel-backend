using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Admin-configurable rate table. Stores per-verification-type pricing.
/// Supports per-institution overrides for volume discounts.
/// </summary>
public class PricingRate : BaseEntity
{
    public Guid? InstitutionId { get; set; } // null = global default rate
    public VerificationType Type { get; set; }
    public decimal PricePerCall { get; set; } // In NGN
    public decimal NimcPartnerCost { get; set; } // Underlying cost from NIMC partner
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    
    // Navigation properties
    public Institution? Institution { get; set; }
}
