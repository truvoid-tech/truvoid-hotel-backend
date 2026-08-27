using System.Security.Claims;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;

namespace TruvoID.API.Middleware;

/// <summary>
/// Middleware that authenticates requests via API key (X-API-Key header).
/// If valid, populates HttpContext.User with the institution and key claims.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeader = "X-API-Key";
    private const string InstitutionClaim = "institution_id";
    private const string ApiKeyClaim = "api_key_id";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeyService)
    {
        // Only process if the API key header is present
        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValues) ||
            string.IsNullOrWhiteSpace(apiKeyValues.First()))
        {
            await _next(context);
            return;
        }

        var rawKey = apiKeyValues.First()!;
        var apiKey = await apiKeyService.ValidateKeyAsync(rawKey);

        if (apiKey is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or revoked API key."
            });
            return;
        }

        // Check rate limits
        if (apiKey.RateLimitPerMinute.HasValue)
        {
            // TODO: Implement Redis-based rate limiting
            // For now, just log that rate limiting is configured
        }

        // Populate claims for downstream authorization
        var claims = new[]
        {
            new Claim(InstitutionClaim, apiKey.InstitutionId.ToString()),
            new Claim(ApiKeyClaim, apiKey.Id.ToString()),
            new Claim("auth_type", "api_key")
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        // Attach for convenience
        context.Items["InstitutionId"] = apiKey.InstitutionId;
        context.Items["ApiKeyId"] = apiKey.Id;

        await _next(context);
    }
}
