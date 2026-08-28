using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class PricingRate : BaseEntity
{
    public Guid? InstitutionId { get; set; }
    public VerificationType Type { get; set; }
    public decimal PricePerCall { get; set; }
    public decimal NimcPartnerCost { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
}
