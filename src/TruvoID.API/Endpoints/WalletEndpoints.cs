using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Endpoints;

public static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        // Institution wallet: balance, transactions, topup initiate
        var walletGroup = app.MapGroup("/v1/wallet")
            .RequireAuthorization();

        walletGroup.MapGet("/balance", GetBalance);
        walletGroup.MapGet("/transactions", GetTransactions);
        walletGroup.MapPost("/topup/initiate", InitiateTopUp);

        // Admin: manual wallet credit, topup approval/rejection
        var adminGroup = app.MapGroup("/v1/admin")
            .RequireAuthorization("TruvoAdmin");

        adminGroup.MapPost("/wallets/{institutionId:guid}/credit", CreditWallet);
        adminGroup.MapPost("/topups/{topupId:guid}/approve", ApproveTopUp);
        adminGroup.MapPost("/topups/{topupId:guid}/reject", RejectTopUp);
        adminGroup.MapGet("/topups/pending", GetPendingTopUps);
        adminGroup.MapGet("/financials", GetFinancials);

        return app;
    }

    // ── Institution endpoints ──────────────────────────────────────────────────

    private static async Task<IResult> GetBalance(
        HttpContext ctx,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var ledger = await db.WalletLedgers
            .Find(l => l.InstitutionId == institutionId)
            .SortByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        var balance = ledger?.BalanceAfter ?? 0m;

        return Results.Ok(new WalletBalanceResponse
        {
            Balance = balance,
            Currency = "NGN"
        });
    }

    private static async Task<IResult> GetTransactions(
        HttpContext ctx,
        [AsParameters] WalletTransactionQuery query,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var filter = Builders<WalletLedger>.Filter.Eq(l => l.InstitutionId, institutionId);

        var docs = await db.WalletLedgers
            .Find(filter)
            .SortByDescending(l => l.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync();

        var total = await db.WalletLedgers.CountDocumentsAsync(filter);

        return Results.Ok(new PaginatedResponse<WalletTransactionResponse>
        {
            Items = docs.Select(MapTransaction).ToList(),
            TotalCount = (int)total,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    private static async Task<IResult> InitiateTopUp(
        HttpContext ctx,
        InitiateTopUpRequest request,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty)
            return Results.Unauthorized();

        var institution = await db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync();
        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        // Since we're not using Paystack/Flutterwave anymore, just record the topup request
        // and let the admin manually credit after receiving payment
        var topupId = Guid.NewGuid();
        var reference = $"TOPUP-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{topupId:N[..8].ToUpperInvariant()}";

        var topup = new WalletTopUp
        {
            Id = topupId,
            InstitutionId = institutionId,
            Amount = request.Amount,
            Reference = reference,
            Status = "Pending",
            PaymentMethod = request.PaymentProvider ?? "manual",
            SubmittedAt = DateTime.UtcNow
        };

        await db.WalletTopUps.InsertOneAsync(topup);

        return Results.Ok(new TopupInitiateResponse
        {
            Reference = reference
            // No AuthorizationUrl since we're doing manual bank transfer
        });
    }

    // ── Admin endpoints ────────────────────────────────────────────────────────

    private static async Task<IResult> CreditWallet(
        Guid institutionId,
        HttpContext ctx,
        CreditWalletRequest request,
        MongoDbContext db,
        INotificationService notifications,
        NotificationFeedService feed,
        TruvoID.Core.Interfaces.IAuditService audit)
    {
        var institution = await db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync();
        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var ledgerId = Guid.NewGuid();
        var prevBalance = 0m;

        var lastLedger = await db.WalletLedgers
            .Find(l => l.InstitutionId == institutionId)
            .SortByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastLedger is not null)
            prevBalance = lastLedger.BalanceAfter;

        var newBalance = prevBalance + request.Amount;

        var ledgerEntry = new WalletLedger
        {
            Id = ledgerId,
            InstitutionId = institutionId,
            Type = "Credit",
            Amount = request.Amount,
            BalanceAfter = newBalance,
            Description = request.Description ?? "Manual credit by admin",
            Reference = request.Reference ?? $"MANUAL-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            ReferenceId = request.ReferenceId,
            CreatedAt = DateTime.UtcNow
        };

        await db.WalletLedgers.InsertOneAsync(ledgerEntry);
        await audit.LogAsync(TruvoID.Domain.Enums.AuditAction.WalletCredited, nameof(Institution), institutionId,
            ctx.GetUserId(), "User", $"₦{request.Amount:N2} — {ledgerEntry.Description}");

        // Send credit notification
        if (!string.IsNullOrWhiteSpace(institution.ContactEmail))
        {
            await notifications.SendAsync(
                institution.ContactEmail,
                institution.Name,
                "Your Wallet Has Been Credited",
                EmailTemplates.TopUpApproved(institution.Name, request.Amount, ledgerEntry.Reference, newBalance));

            await feed.PushAsync(institutionId, "credit", "Wallet Credited",
                $"Your wallet has been credited ₦{request.Amount:N2}. New balance: ₦{newBalance:N2}.", "/dashboard");
        }

        return Results.Ok(new
        {
            message = $"Credited ₦{request.Amount:N2} to {institution.Name}.",
            balanceAfter = newBalance,
            ledgerEntryId = ledgerId
        });
    }

    private static async Task<IResult> ApproveTopUp(
        Guid topupId,
        HttpContext ctx,
        MongoDbContext db,
        INotificationService notifications,
        NotificationFeedService feed,
        TruvoID.Core.Interfaces.IAuditService audit)
    {
        var topup = await db.WalletTopUps.Find(t => t.Id == topupId).FirstOrDefaultAsync();
        if (topup is null)
            return Results.NotFound(new { error = "Top-up request not found." });

        if (topup.Status != "Pending")
            return Results.BadRequest(new { error = "Top-up is not pending." });

        // Credit the wallet
        var institution = await db.Institutions.Find(i => i.Id == topup.InstitutionId).FirstOrDefaultAsync();
        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var prevBalance = 0m;
        var lastLedger = await db.WalletLedgers
            .Find(l => l.InstitutionId == topup.InstitutionId)
            .SortByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastLedger is not null)
            prevBalance = lastLedger.BalanceAfter;

        var newBalance = prevBalance + topup.Amount;

        var ledgerEntry = new WalletLedger
        {
            Id = Guid.NewGuid(),
            InstitutionId = topup.InstitutionId,
            Type = "Credit",
            Amount = topup.Amount,
            BalanceAfter = newBalance,
            Description = $"Top-up approved: {topup.Reference}",
            Reference = topup.Reference,
            ReferenceId = topup.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await db.WalletLedgers.InsertOneAsync(ledgerEntry);

        // Mark topup as approved
        await db.WalletTopUps.UpdateOneAsync(
            t => t.Id == topupId,
            Builders<WalletTopUp>.Update
                .Set(t => t.Status, "Approved")
                .Set(t => t.ApprovedAt, DateTime.UtcNow));
        await audit.LogAsync(TruvoID.Domain.Enums.AuditAction.WalletCredited, nameof(Institution), topup.InstitutionId,
            ctx.GetUserId(), "User", $"Top-up approved: ₦{topup.Amount:N2} ({topup.Reference})");

        // Send credit notification email + in-app notification
        if (!string.IsNullOrWhiteSpace(institution.ContactEmail))
        {
            await notifications.SendAsync(
                institution.ContactEmail,
                institution.Name,
                "Your Wallet Has Been Credited",
                EmailTemplates.TopUpApproved(institution.Name, topup.Amount, topup.Reference, newBalance));

            await feed.PushAsync(institution.Id, "credit", "Wallet Credited",
                $"Your wallet has been credited ₦{topup.Amount:N2}. New balance: ₦{newBalance:N2}.", "/dashboard");
        }

        return Results.Ok(new { message = $"Top-up ₦{topup.Amount:N2} approved and credited to {institution.Name}. New balance: ₦{newBalance:N2}" });
    }

    private static async Task<IResult> RejectTopUp(
        Guid topupId,
        MongoDbContext db)
    {
        var topup = await db.WalletTopUps.Find(t => t.Id == topupId).FirstOrDefaultAsync();
        if (topup is null)
            return Results.NotFound(new { error = "Top-up request not found." });

        await db.WalletTopUps.UpdateOneAsync(
            t => t.Id == topupId,
            Builders<WalletTopUp>.Update
                .Set(t => t.Status, "Rejected")
                .Set(t => t.RejectedAt, DateTime.UtcNow));

        return Results.Ok(new { message = "Top-up request rejected." });
    }

    private static async Task<IResult> GetPendingTopUps(
        MongoDbContext db)
    {
        var pending = await db.WalletTopUps
            .Find(t => t.Status == "Pending")
            .SortByDescending(t => t.SubmittedAt)
            .ToListAsync();

        var result = new List<object>();
        foreach (var t in pending)
        {
            var institution = await db.Institutions.Find(i => i.Id == t.InstitutionId).FirstOrDefaultAsync();
            result.Add(new
            {
                Id = t.Id.ToString(),
                Institution = institution?.Name ?? "Unknown",
                Email = institution?.ContactEmail ?? "",
                Amount = t.Amount,
                Reference = t.Reference,
                Submitted = t.SubmittedAt.ToString("yyyy-MM-dd HH:mm"),
                Status = t.Status
            });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetFinancials(
        MongoDbContext db)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Revenue: sum of all debit transactions this month
        var revenueFilter = Builders<WalletLedger>.Filter.And(
            Builders<WalletLedger>.Filter.Eq(l => l.Type, "Debit"),
            Builders<WalletLedger>.Filter.Gte(l => l.CreatedAt, monthStart)
        );
        var revenue = await db.WalletLedgers
            .Aggregate()
            .Match(revenueFilter)
            .Group(x => 1, g => new { Total = g.Sum(x => x.Amount) })
            .FirstOrDefaultAsync();

        // Costs: sum of all credit transactions this month  
        var costFilter = Builders<WalletLedger>.Filter.And(
            Builders<WalletLedger>.Filter.Eq(l => l.Type, "Credit"),
            Builders<WalletLedger>.Filter.Gte(l => l.CreatedAt, monthStart)
        );
        var costs = await db.WalletLedgers
            .Aggregate()
            .Match(costFilter)
            .Group(x => 1, g => new { Total = g.Sum(x => x.Amount) })
            .FirstOrDefaultAsync();

        // Top-up approval records this month
        var topupFilter = Builders<WalletTopUp>.Filter.Gte(t => t.SubmittedAt, monthStart);
        var topups = await db.WalletTopUps.Find(topupFilter).ToListAsync();

        var grossRevenue = revenue?.Total ?? 0m;
        var nimcPayouts = costs?.Total ?? 0m;
        var netProfit = grossRevenue - nimcPayouts;
        var marginPct = grossRevenue > 0 ? Math.Round(netProfit / grossRevenue * 100, 1) : 0;

        var totalCalls = await db.VerificationCalls.CountDocumentsAsync(_ => true);

        var pendingTopUps = topups.Where(t => t.Status == "Pending").Select(t =>
        {
            var institution = db.Institutions.Find(i => i.Id == t.InstitutionId).FirstOrDefault();
            return new
            {
                Id = t.Id.ToString(),
                Institution = institution?.Name ?? "Unknown",
                Email = institution?.ContactEmail ?? "",
                Amount = t.Amount,
                Reference = t.Reference,
                Submitted = t.SubmittedAt.ToString("yyyy-MM-dd HH:mm"),
                Status = t.Status
            };
        }).ToList();

        // Transaction log
        var thirtyDaysAgo = now.AddDays(-30);
        var txs = await db.WalletLedgers
            .Find(l => l.CreatedAt >= thirtyDaysAgo)
            .SortByDescending(l => l.CreatedAt)
            .Limit(100)
            .ToListAsync();

        var transactions = txs.Select(l =>
        {
            var institution = db.Institutions.Find(i => i.Id == l.InstitutionId).FirstOrDefault();
            return new
            {
                Reference = l.Reference ?? l.Id.ToString("N")[..8],
                Institution = institution?.Name ?? "Unknown",
                Type = l.Type == "Credit" ? "Wallet Top-Up" : "API Call",
                Amount = l.Amount,
                Date = l.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            };
        }).ToList();

        return Results.Ok(new
        {
            GrossRevenue = grossRevenue,
            NimcPayouts = nimcPayouts,
            NetProfit = netProfit,
            MarginPct = marginPct,
            TotalCalls = totalCalls,
            PendingTopUps = pendingTopUps,
            Transactions = transactions
        });
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static WalletTransactionResponse MapTransaction(WalletLedger l)
    {
        var type = l.Type == "Credit" ? WalletTransactionType.Credit : WalletTransactionType.Debit;
        return new WalletTransactionResponse
        {
            Id = l.Id.ToString(),
            Amount = l.Amount,
            BalanceAfter = l.BalanceAfter,
            Type = type,
            Description = l.Description,
            Reference = l.Reference,
            ReferenceId = l.ReferenceId,
            CreatedAt = l.CreatedAt
        };
    }

    // ── request/response models ────────────────────────────────────────────────

    public record WalletTransactionQuery
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 10;
    }

    public record InitiateTopUpRequest
    {
        public decimal Amount { get; init; }
        public string? PaymentProvider { get; init; }
    }

    public record CreditWalletRequest
    {
        public decimal Amount { get; init; }
        public string? Description { get; init; }
        public string? Reference { get; init; }
        public string? ReferenceId { get; init; }
    }
}

// ── Shared DTOs ──────────────────────────────────────────────────────────────

public class WalletBalanceResponse
{
    public decimal Balance { get; init; }
    public string Currency { get; init; } = "NGN";
}

public enum WalletTransactionType { Credit, Debit }

public class WalletTransactionResponse
{
    public string Id { get; init; } = "";
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public WalletTransactionType Type { get; init; }
    public string? Description { get; init; }
    public string? Reference { get; init; }
    public string? ReferenceId { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class TopupInitiateResponse
{
    public string? AuthorizationUrl { get; init; }
    public string? Reference { get; init; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
