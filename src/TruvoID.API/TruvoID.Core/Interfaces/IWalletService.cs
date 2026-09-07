namespace TruvoID.Core.Interfaces;

public interface IWalletService
{
    Task<bool> HasSufficientBalanceAsync(Guid institutionId, decimal amount, CancellationToken ct = default);
    Task<DebitResult> DebitAsync(Guid institutionId, decimal amount, string description, string referenceId, CancellationToken ct = default);
    Task<CreditResult> CreditAsync(Guid institutionId, decimal amount, string description, string referenceId, CancellationToken ct = default);
}

public record DebitResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid LedgerEntryId { get; init; }
    public decimal BalanceAfter { get; init; }
}

public record CreditResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid LedgerEntryId { get; init; }
    public decimal BalanceAfter { get; init; }
}
