using MongoDB.Driver;
using TruvoID.Core.Interfaces;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly MongoDbContext _db;

    public WalletService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasSufficientBalanceAsync(Guid institutionId, decimal amount, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(institutionId, ct);
        return balance >= amount;
    }

    public async Task<DebitResult> DebitAsync(Guid institutionId, decimal amount, string description, string referenceId, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(institutionId, ct);
        if (balance < amount)
            return new DebitResult { Success = false, ErrorMessage = "Insufficient wallet balance.", LedgerEntryId = Guid.Empty, BalanceAfter = balance };

        var ledgerId = Guid.NewGuid();
        var newBalance = balance - amount;

        var entry = new WalletLedger
        {
            Id = ledgerId,
            InstitutionId = institutionId,
            Type = "Debit",
            Amount = amount,
            BalanceAfter = newBalance,
            Description = description,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        await _db.WalletLedgers.InsertOneAsync(entry, cancellationToken: ct);
        return new DebitResult { Success = true, LedgerEntryId = ledgerId, BalanceAfter = newBalance };
    }

    public async Task<CreditResult> CreditAsync(Guid institutionId, decimal amount, string description, string referenceId, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(institutionId, ct);
        var newBalance = balance + amount;

        var ledgerId = Guid.NewGuid();
        var entry = new WalletLedger
        {
            Id = ledgerId,
            InstitutionId = institutionId,
            Type = "Credit",
            Amount = amount,
            BalanceAfter = newBalance,
            Description = description,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        await _db.WalletLedgers.InsertOneAsync(entry, cancellationToken: ct);
        return new CreditResult { Success = true, LedgerEntryId = ledgerId, BalanceAfter = newBalance };
    }

    private async Task<decimal> GetBalanceAsync(Guid institutionId, CancellationToken ct)
    {
        var last = await _db.WalletLedgers
            .Find(l => l.InstitutionId == institutionId)
            .SortByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return last?.BalanceAfter ?? 0m;
    }
}
