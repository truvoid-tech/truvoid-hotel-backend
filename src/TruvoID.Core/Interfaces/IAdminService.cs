using TruvoID.Core.DTOs;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// Platform admin service — internal TruvoID ops dashboard.
/// Provides aggregate data across all institutions.
/// </summary>
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
}
