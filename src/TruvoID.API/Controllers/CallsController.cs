using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/calls")]
public class CallsController : ControllerBase
{
    private readonly ICallHistoryService _callHistoryService;

    public CallsController(ICallHistoryService callHistoryService)
    {
        _callHistoryService = callHistoryService;
    }

    /// <summary>
    /// Get paginated verification call history, filterable by type, status, date range, and staff member.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<CallHistoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalls(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] VerificationType? type = null,
        [FromQuery] VerificationStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var institutionId = GetInstitutionId();

        // Clamp page size
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await _callHistoryService.GetCallsAsync(
            institutionId,
            page,
            pageSize,
            type,
            status,
            fromDate,
            toDate,
            userId,
            ct);

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
}
