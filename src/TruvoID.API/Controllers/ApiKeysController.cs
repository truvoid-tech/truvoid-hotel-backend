using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;

namespace TruvoID.API.Controllers;

[ApiController]
[Route("v1/api-keys")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeysController(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    /// <summary>
    /// List all API keys for the authenticated institution.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ApiKeyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKeys(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var keys = await _apiKeyService.GetKeysAsync(institutionId, ct);
        return Ok(keys);
    }

    /// <summary>
    /// Generate a new API key. The raw key is returned only in this response.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var key = await _apiKeyService.GenerateKeyAsync(institutionId, request.Description, ct);

        return CreatedAtAction(nameof(GetKeys), null, key);
    }

    /// <summary>
    /// Revoke an API key. The key immediately stops authenticating requests.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeKey(Guid id, CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        var revoked = await _apiKeyService.RevokeKeyAsync(institutionId, id, ct);

        if (!revoked)
        {
            return NotFound(new ErrorResponse
            {
                Code = ErrorCodes.NotFound,
                Message = "API key not found or already revoked."
            });
        }

        return NoContent();
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
