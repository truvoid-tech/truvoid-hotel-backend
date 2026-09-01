using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class WalletLedgerEntry : BaseEntity
{
    public Guid InstitutionId { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public decimal Tokens { get; set; }
    public decimal TokensAfter { get; set; }
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
}
