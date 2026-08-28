using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;

namespace TruvoID.API.Controllers;

/// <summary>
/// Platform admin endpoints for internal TruvoID operations.
/// All endpoints require PlatformAdmin role.
/// </summary>
[ApiController]
[Route("v1/admin")]
[Authorize(Roles = "PlatformAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ──────────────────────────── Overview ────────────────────────────

    /// <summary>
    /// Get platform-wide overview: revenue, costs, margin, institution counts, call breakdown.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AdminOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var overview = await _adminService.GetOverviewAsync(ct);
        return Ok(overview);
    }

    // ──────────────────────────── Institutions ────────────────────────────

    /// <summary>
    /// List all institutions with wallet balance, call counts, and status.
    /// </summary>
    [HttpGet("institutions")]
    [ProducesResponseType(typeof(List<AdminInstitutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstitutions(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var institutions = await _adminService.GetInstitutionsAsync(search, status, ct);
        return Ok(institutions);
    }

    /// <summary>
    /// Suspend an institution — blocks all dashboard and API access.
    /// </summary>
    [HttpPost("institutions/{institutionId:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendInstitution(Guid institutionId, CancellationToken ct)
    {
        await _adminService.SuspendInstitutionAsync(institutionId, ct);
        return NoContent();
    }

    /// <summary>
    /// Reactivate a previously suspended institution.
    /// </summary>
    [HttpPost("institutions/{institutionId:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateInstitution(Guid institutionId, CancellationToken ct)
    {
        await _adminService.ReactivateInstitutionAsync(institutionId, ct);
        return NoContent();
    }

    /// <summary>
    /// Approve a pending institution — transitions to Active.
    /// </summary>
    [HttpPost("institutions/{institutionId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveInstitution(Guid institutionId, CancellationToken ct)
    {
        await _adminService.ApproveInstitutionAsync(institutionId, ct);
        return NoContent();
    }

    // ──────────────────────────── Financials ────────────────────────────

    /// <summary>
    /// Get platform-wide financials: revenue, NIMC payouts, pending top-ups, transaction log.
    /// </summary>
    [HttpGet("financials")]
    [ProducesResponseType(typeof(AdminFinancialsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinancials(
        [FromQuery] string period = "mtd",
        CancellationToken ct = default)
    {
        var financials = await _adminService.GetFinancialsAsync(period, ct);
        return Ok(financials);
    }

    /// <summary>
    /// Approve a pending wallet top-up credit.
    /// </summary>
    [HttpPost("topups/{topupId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveTopUp(Guid topupId, CancellationToken ct)
    {
        await _adminService.ApproveTopUpAsync(topupId, ct);
        return NoContent();
    }

    /// <summary>
    /// Reject and remove a pending wallet top-up credit.
    /// </summary>
    [HttpPost("topups/{topupId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectTopUp(Guid topupId, CancellationToken ct)
    {
        await _adminService.RejectTopUpAsync(topupId, ct);
        return NoContent();
    }

    // ──────────────────────────── Pricing ────────────────────────────

    /// <summary>
    /// Get current global pricing rates for all verification types.
    /// </summary>
    [HttpGet("pricing")]
    [ProducesResponseType(typeof(List<AdminPricingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPricing(CancellationToken ct)
    {
        var pricing = await _adminService.GetPricingAsync(ct);
        return Ok(pricing);
    }

    /// <summary>
    /// Update pricing for a verification type (NIN, BVN, Phone).
    /// Deactivates the old rate and creates a new effective rate.
    /// </summary>
    [HttpPut("pricing/{type}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePricing(
        string type,
        [FromBody] UpdatePricingRequest request,
        CancellationToken ct)
    {
        if (request.InstitutionCharge <= 0 || request.NimcCost <= 0)
            return BadRequest(new { code = "INVALID_INPUT", message = "Both InstitutionCharge and NimcCost must be greater than zero." });

        if (request.NimcCost >= request.InstitutionCharge)
            return BadRequest(new { code = "NEGATIVE_MARGIN", message = "NIMC cost must be less than institution charge to maintain a positive margin." });

        await _adminService.UpdatePricingAsync(type, request, ct);
        return NoContent();
    }

    // ──────────────────────────── Admin Management ────────────────────────────

    [HttpGet("admins")]
    public async Task<IActionResult> GetAdmins(CancellationToken ct)
    {
        var admins = await _adminService.GetAdminsAsync(ct);
        return Ok(admins);
    }

    [HttpPost("admins/invite")]
    public async Task<IActionResult> InviteAdmin([FromBody] InviteAdminRequest request, CancellationToken ct)
    {
        try
        {
            var admin = await _adminService.InviteAdminAsync(request, ct);
            return Ok(admin);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ──────────────────────────── Audit Log ────────────────────────────

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var log = await _adminService.GetAuditLogAsync(page, pageSize, ct);
        return Ok(log);
    }
}
