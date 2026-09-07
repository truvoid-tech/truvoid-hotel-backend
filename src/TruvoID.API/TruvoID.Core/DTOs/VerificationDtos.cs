namespace TruvoID.Core.DTOs;

public class VerificationResponse
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CallId { get; set; }
    public decimal WalletBalanceAfter { get; set; }
    public VerificationData? Data { get; set; }
}

public class VerificationData
{
    public string? Name { get; set; }
    public string? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Gender { get; set; }
    public string? PhotoUrl { get; set; }
    public string? StateOfOrigin { get; set; }
    public string? ResidentialAddress { get; set; }
}
