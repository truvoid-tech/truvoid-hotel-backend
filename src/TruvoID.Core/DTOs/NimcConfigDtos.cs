namespace TruvoID.Core.DTOs;

public record NimcEnvironmentDto
{
    public string ActiveEnvironment { get; init; } = "sandbox";
}

public record SetNimcEnvironmentRequest
{
    public string Environment { get; init; } = "sandbox";
}
