using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

public record WalletTransactionResponse
{
    public Guid Id { get; init; }
    public WalletTransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public decimal Tokens { get; init; }
    public string? Description { get; init; }
    public string? ReferenceId { get; init; }
    public DateTime CreatedAt { get; init; }
}
