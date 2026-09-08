using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TruvoID.Domain.Entities;

[BsonIgnoreExtraElements]
public class Institution
{
    [BsonId]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Active, Suspended
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Onboarding: business identity & KYC (step 2) ────────────────────────
    public string? LegalBusinessName { get; set; }
    public string? CacRcNumber { get; set; }
    public string? CacCertificateUrl { get; set; }
    public string? Address { get; set; }
    public string? ExpectedMonthlyVolume { get; set; }
    public string? PrimaryUseCase { get; set; }

    // ── Onboarding: compliance acknowledgment (step 4) ──────────────────────
    public bool ResellerAcknowledged { get; set; }
    public bool DataProcessingAgreed { get; set; }
    public DateTime? ComplianceAcceptedAt { get; set; }

    // ── Onboarding progress ──────────────────────────────────────────────────
    public int OnboardingStep { get; set; } = 1;
    public bool OnboardingComplete { get; set; }
}
