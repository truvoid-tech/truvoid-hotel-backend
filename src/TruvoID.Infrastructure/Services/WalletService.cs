using MongoDB.Driver;
using TruvoID.Core.Constants;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly MongoDbContext _db;

    public WalletService(MongoDbContext db) => _db = db;

    public async Task<WalletBalanceResponse?> GetBalanceAsync(Guid institutionId, CancellationToken ct = default)
    {
        var filter = Builders<WalletLedgerEntry>.Filter.Eq(e => e.InstitutionId, institutionId);
        var sort = Builders<WalletLedgerEntry>.Sort.Descending(e => e.CreatedAt);
        var lastEntry = await _db.WalletLedgerEntries.Find(filter).Sort(sort).FirstOrDefaultAsync(ct);

        if (lastEntry is null)
        {
            return new WalletBalanceResponse
            {
                Balance = 0,
                InstitutionId = institutionId,
                LastUpdated = DateTime.UtcNow
            };
        }

        return new WalletBalanceResponse
        {
            Balance = lastEntry.BalanceAfter,
            Tokens = lastEntry.TokensAfter,
            InstitutionId = institutionId,
            LastUpdated = lastEntry.CreatedAt
        };
    }

    public async Task<bool> HasSufficientBalanceAsync(Guid institutionId, decimal requiredAmount, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(institutionId, ct);
        return balance is not null && balance.Balance >= requiredAmount;
    }

    public async Task<WalletLedgerEntryResult> DebitAsync(Guid institutionId, decimal amount, string description, string? idempotencyKey = null, CancellationToken ct = default)
    {
        // Check idempotency
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _db.WalletLedgerEntries
                .Find(e => e.ReferenceId == idempotencyKey && e.InstitutionId == institutionId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                return new WalletLedgerEntryResult
                {
                    Success = true,
                    LedgerEntryId = existing.Id,
                    BalanceAfter = existing.BalanceAfter
                };
            }
        }

        // Get current balance
        var filter = Builders<WalletLedgerEntry>.Filter.Eq(e => e.InstitutionId, institutionId);
        var sort = Builders<WalletLedgerEntry>.Sort.Descending(e => e.CreatedAt);
        var lastEntry = await _db.WalletLedgerEntries.Find(filter).Sort(sort).FirstOrDefaultAsync(ct);
        var currentBalance = lastEntry?.BalanceAfter ?? 0m;
        var currentTokens = lastEntry?.TokensAfter ?? 0m;

        if (currentBalance < amount)
        {
            return new WalletLedgerEntryResult
            {
                Success = false,
                ErrorMessage = "Insufficient wallet balance."
            };
        }

        var tokens = amount / TokenPricing.NairaPerToken;

        var entry = new WalletLedgerEntry
        {
            InstitutionId = institutionId,
            Type = WalletTransactionType.Debit,
            Amount = amount,
            BalanceAfter = currentBalance - amount,
            Tokens = tokens,
            TokensAfter = currentTokens - tokens,
            ReferenceId = idempotencyKey,
            Description = description
        };

        await _db.WalletLedgerEntries.InsertOneAsync(entry, cancellationToken: ct);

        return new WalletLedgerEntryResult
        {
            Success = true,
            LedgerEntryId = entry.Id,
            BalanceAfter = entry.BalanceAfter
        };
    }

    public async Task<WalletLedgerEntryResult> CreditAsync(Guid institutionId, decimal amount, string description, string? referenceId = null, CancellationToken ct = default)
    {
        var filter = Builders<WalletLedgerEntry>.Filter.Eq(e => e.InstitutionId, institutionId);
        var sort = Builders<WalletLedgerEntry>.Sort.Descending(e => e.CreatedAt);
        var lastEntry = await _db.WalletLedgerEntries.Find(filter).Sort(sort).FirstOrDefaultAsync(ct);
        var currentBalance = lastEntry?.BalanceAfter ?? 0m;
        var currentTokens = lastEntry?.TokensAfter ?? 0m;
        var tokens = amount / TokenPricing.NairaPerToken;

        var entry = new WalletLedgerEntry
        {
            InstitutionId = institutionId,
            Type = WalletTransactionType.Credit,
            Amount = amount,
            BalanceAfter = currentBalance + amount,
            Tokens = tokens,
            TokensAfter = currentTokens + tokens,
            ReferenceId = referenceId,
            Description = description
        };

        await _db.WalletLedgerEntries.InsertOneAsync(entry, cancellationToken: ct);

        return new WalletLedgerEntryResult
        {
            Success = true,
            LedgerEntryId = entry.Id,
            BalanceAfter = entry.BalanceAfter
        };
    }

    public async Task<bool> ReverseAsync(Guid ledgerEntryId, string reason, CancellationToken ct = default)
    {
        var original = await _db.WalletLedgerEntries
            .Find(e => e.Id == ledgerEntryId)
            .FirstOrDefaultAsync(ct);

        if (original is null) return false;

        var reversal = new WalletLedgerEntry
        {
            InstitutionId = original.InstitutionId,
            Type = WalletTransactionType.Refund,
            Amount = original.Amount,
            BalanceAfter = original.Type == WalletTransactionType.Debit
                ? original.BalanceAfter + original.Amount
                : original.BalanceAfter - original.Amount,
            Tokens = original.Tokens,
            TokensAfter = original.Type == WalletTransactionType.Debit
                ? original.TokensAfter + original.Tokens
                : original.TokensAfter - original.Tokens,
            ReferenceId = original.Id.ToString(),
            Description = $"Reversal: {reason}"
        };

        await _db.WalletLedgerEntries.InsertOneAsync(reversal, cancellationToken: ct);
        return true;
    }

    public async Task<List<WalletTransactionResponse>> GetTransactionsAsync(Guid institutionId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var entries = await _db.WalletLedgerEntries
            .Find(e => e.InstitutionId == institutionId)
            .SortByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return entries.Select(e => new WalletTransactionResponse
        {
            Id = e.Id,
            Type = e.Type,
            Amount = e.Amount,
            BalanceAfter = e.BalanceAfter,
            Tokens = e.Tokens,
            Description = e.Description,
            ReferenceId = e.ReferenceId,
            CreatedAt = e.CreatedAt
        }).ToList();
    }
}
