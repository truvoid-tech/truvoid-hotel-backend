namespace TruvoID.Core.DTOs;

public record VerifyNinRequest
{
    public string Nin { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
}

public record VerifyBvnRequest
{
    public string Bvn { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
}

public record VerifyPhoneRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
}
