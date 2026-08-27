namespace TruvoID.Core.DTOs;

public record ErrorResponse
{
    public string Code { get; init; } = string.Empty; // INSUFFICIENT_BALANCE, INVALID_INPUT, etc.
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}

/// <summary>
/// Standard error codes used across the API.
/// </summary>
public static class ErrorCodes
{
    public const string InsufficientBalance = "INSUFFICIENT_BALANCE";
    public const string InvalidInput = "INVALID_INPUT";
    public const string UpstreamTimeout = "UPSTREAM_TIMEOUT";
    public const string UpstreamError = "UPSTREAM_ERROR";
    public const string ApiKeyRevoked = "API_KEY_REVOKED";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InternalError = "INTERNAL_ERROR";
}
