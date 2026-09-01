using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;

namespace TruvoID.API.Controllers;

[Authorize]
[ApiController]
[Route("v1/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    /// <summary>
    /// Get current onboarding progress for the institution.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(OnboardingStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var status = await _onboardingService.GetStatusAsync(institutionId, ct);
        return Ok(status);
    }

    /// <summary>
    /// Step 1: Update institution profile (name, contact email, phone).
    /// </summary>
    [HttpPut("institution")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateInstitution([FromBody] InstitutionSetupRequest request, CancellationToken ct)
    {
        try
        {
            var institutionId = GetInstitutionId();
            await _onboardingService.UpdateInstitutionAsync(institutionId, request, ct);
            return Ok(new { message = "Institution profile updated.", nextStep = 2 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse { Code = ErrorCodes.InvalidInput, Message = ex.Message });
        }
    }

    /// <summary>
    /// Step 2: Update business verification details (CAC, address, volume, use case).
    /// </summary>
    [HttpPut("business")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBusinessInfo([FromBody] BusinessInfoRequest request, CancellationToken ct)
    {
        try
        {
            var institutionId = GetInstitutionId();
            await _onboardingService.UpdateBusinessInfoAsync(institutionId, request, ct);
            return Ok(new { message = "Business info updated.", nextStep = 3 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse { Code = ErrorCodes.InvalidInput, Message = ex.Message });
        }
    }

    /// <summary>
    /// Step 4: Accept compliance terms (reseller acknowledgment + data processing).
    /// </summary>
    [HttpPost("compliance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptCompliance([FromBody] ComplianceAcceptanceRequest request, CancellationToken ct)
    {
        try
        {
            var institutionId = GetInstitutionId();
            await _onboardingService.AcceptComplianceAsync(institutionId, request, ct);
            return Ok(new { message = "Compliance accepted.", nextStep = 5 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Step 6: Invite a staff member to the institution.
    /// </summary>
    [HttpPost("staff")]
    [ProducesResponseType(typeof(StaffInviteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteStaff([FromBody] StaffInviteRequest request, CancellationToken ct)
    {
        try
        {
            var institutionId = GetInstitutionId();
            var result = await _onboardingService.InviteStaffAsync(institutionId, request, ct);
            return CreatedAtAction(nameof(GetStaff), null, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get all staff members for the institution.
    /// </summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(List<StaffInviteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaff(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var staff = await _onboardingService.GetStaffAsync(institutionId, ct);
        return Ok(staff);
    }

    /// <summary>
    /// Remove a staff member from the institution.
    /// </summary>
    [HttpDelete("staff/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStaff(Guid userId, CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var removed = await _onboardingService.RemoveStaffAsync(institutionId, userId, ct);

        if (!removed)
        {
            return NotFound(new ErrorResponse
            {
                Code = ErrorCodes.NotFound,
                Message = "Staff member not found."
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Complete the onboarding process. Marks institution as active.
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken ct)
    {
        try
        {
            var institutionId = GetInstitutionId();
            await _onboardingService.CompleteOnboardingAsync(institutionId, ct);
            return Ok(new { message = "Onboarding completed. Institution is now active." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = ex.Message
            });
        }
    }

    private Guid GetInstitutionId()
    {
        var claim = User.FindFirst("institution_id")
            ?? throw new UnauthorizedAccessException("No institution context found.");
        return Guid.Parse(claim.Value);
    }
}
