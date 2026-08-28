namespace TruvoID.Core.DTOs;

public record NimcConfigDto
{
    public string Environment { get; init; } = "sandbox";
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string? PartnerId { get; init; }
    public bool IsActive { get; init; }
}

public record UpdateNimcConfigRequest
{
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string? PartnerId { get; init; }
    public string? SecretKey { get; init; }
    public bool IsActive { get; init; }
}
