using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public InstitutionType Type { get; set; }
    public InstitutionStatus Status { get; set; } = InstitutionStatus.PendingActivation;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? CacRcNumber { get; set; }
    public string? CacCertificateUrl { get; set; }
    public string? ExpectedMonthlyVolume { get; set; }
    public string? PrimaryUseCase { get; set; }
    public bool ComplianceAccepted { get; set; }
    public DateTime? ComplianceAcceptedAt { get; set; }
    public bool ResellerAcknowledged { get; set; }
    public bool DataProcessingAgreed { get; set; }
    public int OnboardingStep { get; set; } = 1;
    public bool OnboardingCompleted { get; set; }
    public string? BrandingMetadataJson { get; set; }
}
