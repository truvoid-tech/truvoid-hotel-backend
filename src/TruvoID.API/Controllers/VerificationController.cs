using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/verify")]
public class VerificationController : ControllerBase
{
    private readonly IVerificationService _verificationService;

    public VerificationController(IVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    /// <summary>
    /// Verify a NIN (National Identification Number).
    /// </summary>
    [HttpPost("nin")]
    [ProducesResponseType(typeof(VerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> VerifyNin([FromBody] VerifyNinRequest request, CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var apiKeyId = GetApiKeyId();
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Nin))
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = "NIN is required."
            });
        }

        var result = await _verificationService.VerifyAsync(
            institutionId,
            VerificationType.Nin,
            request.Nin,
            userId,
            apiKeyId,
            request.IdempotencyKey,
            ct);

        if (result.ErrorCode == ErrorCodes.InsufficientBalance)
            return StatusCode(StatusCodes.Status402PaymentRequired, result);

        if (result.ErrorCode is ErrorCodes.InternalError or ErrorCodes.UpstreamError or ErrorCodes.UpstreamTimeout)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return Ok(result);
    }

    /// <summary>
    /// Verify a BVN (Bank Verification Number).
    /// </summary>
    [HttpPost("bvn")]
    [ProducesResponseType(typeof(VerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> VerifyBvn([FromBody] VerifyBvnRequest request, CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var apiKeyId = GetApiKeyId();
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Bvn))
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = "BVN is required."
            });
        }

        var result = await _verificationService.VerifyAsync(
            institutionId,
            VerificationType.Bvn,
            request.Bvn,
            userId,
            apiKeyId,
            request.IdempotencyKey,
            ct);

        if (result.ErrorCode == ErrorCodes.InsufficientBalance)
            return StatusCode(StatusCodes.Status402PaymentRequired, result);

        if (result.ErrorCode is ErrorCodes.InternalError or ErrorCodes.UpstreamError or ErrorCodes.UpstreamTimeout)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return Ok(result);
    }

    /// <summary>
    /// Verify a phone number.
    /// </summary>
    [HttpPost("phone")]
    [ProducesResponseType(typeof(VerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request, CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var apiKeyId = GetApiKeyId();
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new ErrorResponse
            {
                Code = ErrorCodes.InvalidInput,
                Message = "Phone number is required."
            });
        }

        var result = await _verificationService.VerifyAsync(
            institutionId,
            VerificationType.Phone,
            request.PhoneNumber,
            userId,
            apiKeyId,
            request.IdempotencyKey,
            ct);

        if (result.ErrorCode == ErrorCodes.InsufficientBalance)
            return StatusCode(StatusCodes.Status402PaymentRequired, result);

        if (result.ErrorCode is ErrorCodes.InternalError or ErrorCodes.UpstreamError or ErrorCodes.UpstreamTimeout)
            return StatusCode(StatusCodes.Status502BadGateway, result);

        return Ok(result);
    }

    private Guid GetInstitutionId()
    {
        if (HttpContext.Items["InstitutionId"] is Guid id)
            return id;

        if (User.FindFirstValue("institution_id") is string idStr && Guid.TryParse(idStr, out var parsed))
            return parsed;

        throw new UnauthorizedAccessException("No institution context found.");
    }

    private Guid? GetApiKeyId()
    {
        if (HttpContext.Items["ApiKeyId"] is Guid id)
            return id;
        return null;
    }

    private Guid? GetUserId()
    {
        if (User.FindFirstValue("user_id") is string idStr && Guid.TryParse(idStr, out var parsed))
            return parsed;
        return null;
    }
}
