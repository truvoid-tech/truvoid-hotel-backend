using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Append-only wallet ledger. Source of truth for balance.
/// Each entry represents a single credit, debit, or refund transaction.
/// </summary>
public class WalletLedgerEntry : BaseEntity
{
    public Guid InstitutionId { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceId { get; set; } // Payment gateway reference or verification call ID
    public string? Description { get; set; }
    
    // Navigation properties
    public Institution Institution { get; set; } = null!;
}
