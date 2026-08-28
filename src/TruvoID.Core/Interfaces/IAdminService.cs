using TruvoID.Core.DTOs;

namespace TruvoID.Core.Interfaces;

public interface IAdminService
{
    // Overview
    Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default);

    // Institutions
    Task<List<AdminInstitutionDto>> GetInstitutionsAsync(string? search = null, string? status = null, CancellationToken ct = default);
    Task SuspendInstitutionAsync(Guid institutionId, CancellationToken ct = default);
    Task ReactivateInstitutionAsync(Guid institutionId, CancellationToken ct = default);
    Task ApproveInstitutionAsync(Guid institutionId, CancellationToken ct = default);

    // Financials
    Task<AdminFinancialsDto> GetFinancialsAsync(string period = "mtd", CancellationToken ct = default);

    // Top-ups
    Task ApproveTopUpAsync(Guid topupId, CancellationToken ct = default);
    Task RejectTopUpAsync(Guid topupId, CancellationToken ct = default);

    // Pricing
    Task<List<AdminPricingDto>> GetPricingAsync(CancellationToken ct = default);
    Task UpdatePricingAsync(string type, UpdatePricingRequest request, CancellationToken ct = default);

    // Admin Management
    Task<List<AdminUserDto>> GetAdminsAsync(CancellationToken ct = default);
    Task<AdminUserDto> InviteAdminAsync(InviteAdminRequest request, CancellationToken ct = default);
    Task<AdminUserDto> UpdateAdminRoleAsync(Guid userId, string newRole, CancellationToken ct = default);

    // Audit Log
    Task<List<AuditLogEntryDto>> GetAuditLogAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

    // Low Balance Alerts
    Task<List<LowBalanceAlertDto>> GetLowBalanceAlertsAsync(decimal threshold = 5000m, CancellationToken ct = default);
    Task SendLowBalanceNotificationAsync(Guid institutionId, CancellationToken ct = default);

    // Direct Wallet Credit (for testing/admin use)
    Task CreditInstitutionWalletAsync(Guid institutionId, decimal amount, string description, CancellationToken ct = default);
}
