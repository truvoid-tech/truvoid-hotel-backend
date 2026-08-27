using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Top-level tenant entity. Owns wallet, users, and API keys.
/// </summary>
public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public InstitutionType Type { get; set; }
    public InstitutionStatus Status { get; set; } = InstitutionStatus.PendingActivation;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    
    // Business verification fields
    public string? CacRcNumber { get; set; }
    public string? CacCertificateUrl { get; set; }
    public string? ExpectedMonthlyVolume { get; set; } // tier1, tier2, etc.
    public string? PrimaryUseCase { get; set; } // kyc, aml, identity, other
    
    // Compliance
    public bool ComplianceAccepted { get; set; }
    public DateTime? ComplianceAcceptedAt { get; set; }
    public bool ResellerAcknowledged { get; set; }
    public bool DataProcessingAgreed { get; set; }
    
    // Onboarding progress
    public int OnboardingStep { get; set; } = 1; // 1-7
    public bool OnboardingCompleted { get; set; }
    
    // Branding metadata (JSON-serializable, stored as text)
    public string? BrandingMetadataJson { get; set; }
    
    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<WalletLedgerEntry> WalletLedgerEntries { get; set; } = new List<WalletLedgerEntry>();
    public ICollection<VerificationCall> VerificationCalls { get; set; } = new List<VerificationCall>();
    public ICollection<PricingRate> PricingRates { get; set; } = new List<PricingRate>();
}
