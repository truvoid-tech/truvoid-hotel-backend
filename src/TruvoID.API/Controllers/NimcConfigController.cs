using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/admin/nimc")]
[Authorize(Roles = "PlatformAdmin")]
public class NimcConfigController : ControllerBase
{
    private readonly INimcConfigService _nimcConfigService;

    public NimcConfigController(INimcConfigService nimcConfigService)
    {
        _nimcConfigService = nimcConfigService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var config = await _nimcConfigService.GetActiveEnvironmentAsync(ct);
        return Ok(config);
    }

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SetNimcEnvironmentRequest request, CancellationToken ct)
    {
        if (request.Environment != "live" && request.Environment != "sandbox")
            return BadRequest(new { message = "Environment must be 'live' or 'sandbox'." });

        await _nimcConfigService.SetActiveEnvironmentAsync(request.Environment, ct);
        return Ok(new { message = $"NIMC environment set to {request.Environment}.", activeEnvironment = request.Environment });
    }
}
