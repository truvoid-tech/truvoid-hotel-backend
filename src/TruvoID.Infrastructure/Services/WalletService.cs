using Microsoft.EntityFrameworkCore;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly TruvoIDDbContext _db;

    public WalletService(TruvoIDDbContext db)
    {
        _db = db;
    }

    public async Task<WalletBalanceResponse?> GetBalanceAsync(Guid institutionId, CancellationToken ct = default)
    {
        var lastEntry = await _db.WalletLedgerEntries
            .Where(e => e.InstitutionId == institutionId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

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
                .FirstOrDefaultAsync(e => e.ReferenceId == idempotencyKey && e.InstitutionId == institutionId, ct);

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

        // Use transaction for atomic debit + balance update
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var lastEntry = await _db.WalletLedgerEntries
                .Where(e => e.InstitutionId == institutionId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var currentBalance = lastEntry?.BalanceAfter ?? 0m;

            if (currentBalance < amount)
            {
                await transaction.RollbackAsync(ct);
                return new WalletLedgerEntryResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient wallet balance."
                };
            }

            var entry = new TruvoID.Domain.Entities.WalletLedgerEntry
            {
                InstitutionId = institutionId,
                Type = TruvoID.Domain.Enums.WalletTransactionType.Debit,
                Amount = amount,
                BalanceAfter = currentBalance - amount,
                ReferenceId = idempotencyKey,
                Description = description
            };

            _db.WalletLedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new WalletLedgerEntryResult
            {
                Success = true,
                LedgerEntryId = entry.Id,
                BalanceAfter = entry.BalanceAfter
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WalletLedgerEntryResult> CreditAsync(Guid institutionId, decimal amount, string description, string? referenceId = null, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var lastEntry = await _db.WalletLedgerEntries
                .Where(e => e.InstitutionId == institutionId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var currentBalance = lastEntry?.BalanceAfter ?? 0m;

            var entry = new TruvoID.Domain.Entities.WalletLedgerEntry
            {
                InstitutionId = institutionId,
                Type = TruvoID.Domain.Enums.WalletTransactionType.Credit,
                Amount = amount,
                BalanceAfter = currentBalance + amount,
                ReferenceId = referenceId,
                Description = description
            };

            _db.WalletLedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new WalletLedgerEntryResult
            {
                Success = true,
                LedgerEntryId = entry.Id,
                BalanceAfter = entry.BalanceAfter
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> ReverseAsync(Guid ledgerEntryId, string reason, CancellationToken ct = default)
    {
        var original = await _db.WalletLedgerEntries.FindAsync(new object[] { ledgerEntryId }, ct);
        if (original is null) return false;

        // Create reversal entry
        var reversal = new TruvoID.Domain.Entities.WalletLedgerEntry
        {
            InstitutionId = original.InstitutionId,
            Type = TruvoID.Domain.Enums.WalletTransactionType.Refund,
            Amount = original.Amount,
            BalanceAfter = original.Type == TruvoID.Domain.Enums.WalletTransactionType.Debit
                ? original.BalanceAfter + original.Amount
                : original.BalanceAfter - original.Amount,
            ReferenceId = original.Id.ToString(),
            Description = $"Reversal: {reason}"
        };

        _db.WalletLedgerEntries.Add(reversal);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<WalletTransactionResponse>> GetTransactionsAsync(Guid institutionId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        return await _db.WalletLedgerEntries
            .Where(e => e.InstitutionId == institutionId)
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new WalletTransactionResponse
            {
                Id = e.Id,
                Type = e.Type,
                Amount = e.Amount,
                BalanceAfter = e.BalanceAfter,
                Description = e.Description,
                ReferenceId = e.ReferenceId,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);
    }
}
