namespace TruvoID.Core.DTOs;

public record TopupInitiateRequest
{
    public decimal Amount { get; init; }
    public string PaymentProvider { get; init; } = "paystack"; // "paystack" or "flutterwave"
    public string? CallbackUrl { get; init; }
}

public record TopupInitiateResponse
{
    public string AuthorizationUrl { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}
