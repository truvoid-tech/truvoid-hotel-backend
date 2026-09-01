namespace TruvoID.Core.DTOs;

public record WalletBalanceResponse
{
    public decimal Balance { get; init; }
    public decimal Tokens { get; init; }
    public Guid InstitutionId { get; init; }
    public DateTime LastUpdated { get; init; }
}
