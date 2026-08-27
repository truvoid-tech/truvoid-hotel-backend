using TruvoID.Core.DTOs;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// Wallet service for prepaid credit management.
/// Handles balance checks, debits, credits, and reversals.
/// </summary>
public interface IWalletService
{
    Task<WalletBalanceResponse?> GetBalanceAsync(Guid institutionId, CancellationToken ct = default);
    Task<bool> HasSufficientBalanceAsync(Guid institutionId, decimal requiredAmount, CancellationToken ct = default);
    Task<WalletLedgerEntryResult> DebitAsync(Guid institutionId, decimal amount, string description, string? idempotencyKey = null, CancellationToken ct = default);
    Task<WalletLedgerEntryResult> CreditAsync(Guid institutionId, decimal amount, string description, string? referenceId = null, CancellationToken ct = default);
    Task<bool> ReverseAsync(Guid ledgerEntryId, string reason, CancellationToken ct = default);
    Task<List<WalletTransactionResponse>> GetTransactionsAsync(Guid institutionId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public record WalletLedgerEntryResult
{
    public bool Success { get; init; }
    public Guid? LedgerEntryId { get; init; }
    public decimal BalanceAfter { get; init; }
    public string? ErrorMessage { get; init; }
}
