using TruvoID.Core.DTOs;

namespace TruvoID.Core.Interfaces;

public interface IOnboardingService
{
    Task<OnboardingStatusResponse> GetStatusAsync(Guid institutionId, CancellationToken ct = default);
    Task UpdateInstitutionAsync(Guid institutionId, InstitutionSetupRequest request, CancellationToken ct = default);
    Task UpdateBusinessInfoAsync(Guid institutionId, BusinessInfoRequest request, CancellationToken ct = default);
    Task AcceptComplianceAsync(Guid institutionId, ComplianceAcceptanceRequest request, CancellationToken ct = default);
    Task<StaffInviteResponse> InviteStaffAsync(Guid institutionId, StaffInviteRequest request, CancellationToken ct = default);
    Task<bool> RemoveStaffAsync(Guid institutionId, Guid userId, CancellationToken ct = default);
    Task<List<StaffInviteResponse>> GetStaffAsync(Guid institutionId, CancellationToken ct = default);
    Task CompleteOnboardingAsync(Guid institutionId, CancellationToken ct = default);
}
