using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

public record VerificationResponse
{
    public string Status { get; init; } = string.Empty; // "match" | "no_match" | "error"
    public VerificationData? Data { get; init; }
    public string? CallId { get; init; }
    public decimal WalletBalanceAfter { get; init; }
    public string? ErrorCode { get; init; } // INSUFFICIENT_BALANCE, INVALID_INPUT, UPSTREAM_TIMEOUT, etc.
    public string? ErrorMessage { get; init; }
}

public record VerificationData
{
    public string? Name { get; init; }
    public string? DateOfBirth { get; init; }
    public string? PhotoUrl { get; init; }
    public Dictionary<string, string>? AdditionalFields { get; init; }
}
