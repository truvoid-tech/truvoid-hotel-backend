using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

// ─── Step 1: Institution Profile ───
public record InstitutionSetupRequest
{
    public string Name { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
}

// ─── Step 2: Business Verification ───
public record BusinessInfoRequest
{
    public string? LegalBusinessName { get; init; }
    public InstitutionType Type { get; init; }
    public string? CacRcNumber { get; init; }
    public string? Address { get; init; }
    public string? ExpectedMonthlyVolume { get; init; }
    public string? PrimaryUseCase { get; init; }
}

// ─── Step 4: Compliance Acknowledgment ───
public record ComplianceAcceptanceRequest
{
    public bool ResellerAcknowledged { get; init; }
    public bool DataProcessingAgreed { get; init; }
}

// ─── Step 6: Staff Invitations ───
public record StaffInviteRequest
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.Staff;
    public int? DailyCallLimit { get; init; }
}

public record StaffInviteResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public UserStatus Status { get; init; }
}

// ─── Onboarding Status ───
public record OnboardingStatusResponse
{
    public int CurrentStep { get; init; }
    public bool IsCompleted { get; init; }
    public InstitutionOnboardingInfo Institution { get; init; } = null!;
}

public record InstitutionOnboardingInfo
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public InstitutionStatus Status { get; init; }
    public bool BusinessInfoSubmitted { get; init; }
    public bool ComplianceAccepted { get; init; }
    public bool WalletFunded { get; init; }
    public int StaffCount { get; init; }
}
