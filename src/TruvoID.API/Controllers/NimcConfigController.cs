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
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await _nimcConfigService.GetAllAsync(ct);
        return Ok(configs);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var config = await _nimcConfigService.GetActiveAsync(ct);
        return config is null ? NotFound(new { message = "No active NIMC configuration found." }) : Ok(config);
    }

    [HttpPut("{environment}")]
    public async Task<IActionResult> Upsert(string environment, [FromBody] UpdateNimcConfigRequest request, CancellationToken ct)
    {
        if (environment != "live" && environment != "sandbox")
            return BadRequest(new { message = "Environment must be 'live' or 'sandbox'." });

        await _nimcConfigService.UpsertAsync(environment, request, ct);
        return Ok(new { message = $"NIMC {environment} configuration saved." });
    }

    [HttpPost("{environment}/activate")]
    public async Task<IActionResult> Activate(string environment, CancellationToken ct)
    {
        if (environment != "live" && environment != "sandbox")
            return BadRequest(new { message = "Environment must be 'live' or 'sandbox'." });

        await _nimcConfigService.ActivateAsync(environment, ct);
        return Ok(new { message = $"NIMC {environment} environment activated." });
    }
}
