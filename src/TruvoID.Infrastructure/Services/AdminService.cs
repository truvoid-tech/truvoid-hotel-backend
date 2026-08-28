using Microsoft.EntityFrameworkCore;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly TruvoIDDbContext _db;

    public AdminService(TruvoIDDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────── Overview ────────────────────────────

    public async Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        // Institution counts
        var allInstitutions = await _db.Institutions
            .Select(i => new { i.Id, i.Status, i.CreatedAt, i.ContactEmail, i.Name })
            .ToListAsync(ct);

        var activeCount = allInstitutions.Count(i => i.Status == InstitutionStatus.Active);
        var pendingCount = allInstitutions.Count(i => i.Status == InstitutionStatus.PendingActivation);
        var newThisMonth = allInstitutions.Count(i => i.CreatedAt >= monthStart);

        // Verification calls MTD
        var callsThisMonth = await _db.VerificationCalls
            .Where(v => v.CreatedAt >= monthStart)
            .ToListAsync(ct);

        var callsLastMonth = await _db.VerificationCalls
            .Where(v => v.CreatedAt >= lastMonthStart && v.CreatedAt < monthStart)
            .ToListAsync(ct);

        // Revenue & costs MTD — from wallet debits
        var debitsThisMonth = await _db.WalletLedgerEntries
            .Where(w => w.Type == WalletTransactionType.Debit && w.CreatedAt >= monthStart)
            .ToListAsync(ct);

        var debitsLastMonth = await _db.WalletLedgerEntries
            .Where(w => w.Type == WalletTransactionType.Debit && w.CreatedAt >= lastMonthStart && w.CreatedAt < monthStart)
            .ToListAsync(ct);

        var revenueMtd = debitsThisMonth.Sum(d => Math.Abs(d.Amount));
        var revenueLastMonth = debitsLastMonth.Sum(d => Math.Abs(d.Amount));

        // Get global NIMC cost per type
        var globalPricing = await _db.PricingRates
            .Where(r => r.InstitutionId == null && r.IsActive)
            .ToListAsync(ct);

        var callsByType = callsThisMonth.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
        var totalNimcCost = 0m;
        foreach (var kvp in callsByType)
        {
            var rate = globalPricing.FirstOrDefault(r => r.Type == kvp.Key);
            totalNimcCost += (rate?.NimcPartnerCost ?? 0) * kvp.Value;
        }

        // Wallet balances
        var totalWalletBalances = await _db.WalletLedgerEntries
            .GroupBy(w => w.InstitutionId)
            .Select(g => g.OrderByDescending(w => w.CreatedAt).First().BalanceAfter)
            .SumAsync(ct);

        // Pending top-ups (credits with description containing "topup")
        var pendingTopUps = await _db.WalletLedgerEntries
            .Where(w => w.Type == WalletTransactionType.Credit && w.Description != null && w.Description.Contains("pending"))
            .CountAsync(ct);

        // Revenue growth
        var growthPct = revenueLastMonth > 0
            ? Math.Round((revenueMtd - revenueLastMonth) / revenueLastMonth * 100, 1)
            : 0m;

        // Top institutions by call volume
        var topInstData = callsThisMonth
            .GroupBy(c => c.InstitutionId)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var topInstIds = topInstData.Select(g => g.Key).ToList();
        var topInstDetails = await _db.Institutions
            .Where(i => topInstIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var topInstitutions = topInstData.Select(g =>
        {
            var inst = topInstDetails.GetValueOrDefault(g.Key);
            var callCount = g.Count();
            var revForInst = debitsThisMonth
                .Where(d => d.InstitutionId == g.Key)
                .Sum(d => Math.Abs(d.Amount));

            return new InstitutionVolumeDto
            {
                Id = g.Key,
                Name = inst?.Name ?? "Unknown",
                Email = inst?.ContactEmail ?? "",
                CallsMtd = callCount,
                RevenueMtd = revForInst,
                Active = inst?.Status == InstitutionStatus.Active
            };
        }).ToList();

        // Call breakdown
        var ninCount = callsThisMonth.Count(c => c.Type == VerificationType.Nin);
        var bvnCount = callsThisMonth.Count(c => c.Type == VerificationType.Bvn);
        var phoneCount = callsThisMonth.Count(c => c.Type == VerificationType.Phone);

        return new AdminOverviewDto
        {
            RevenueMtd = revenueMtd,
            CostsMtd = totalNimcCost,
            NetMargin = revenueMtd - totalNimcCost,
            ActiveInstitutions = activeCount,
            PendingInstitutions = pendingCount,
            TotalApiCallsMtd = callsThisMonth.Count,
            TotalWalletBalances = totalWalletBalances,
            PendingTopUpApprovals = pendingTopUps,
            RevenueGrowthPct = growthPct,
            NewInstitutionsThisMonth = newThisMonth,
            TopInstitutions = topInstitutions,
            CallBreakdown = new CallBreakdownDto
            {
                NinCalls = ninCount,
                BvnCalls = bvnCount,
                PhoneCalls = phoneCount
            }
        };
    }

    // ──────────────────────────── Institutions ────────────────────────────

    public async Task<List<AdminInstitutionDto>> GetInstitutionsAsync(
        string? search = null, string? status = null, CancellationToken ct = default)
    {
        var query = _db.Institutions
            .Include(i => i.Users)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(term) ||
                (i.ContactEmail != null && i.ContactEmail.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<InstitutionStatus>(status, true, out var statusEnum))
                query = query.Where(i => i.Status == statusEnum);
        }

        var institutions = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        // Get call counts and wallet balances in bulk
        var instIds = institutions.Select(i => i.Id).ToList();

        var callCounts = await _db.VerificationCalls
            .Where(v => instIds.Contains(v.InstitutionId) && v.CreatedAt >= GetMonthStart())
            .GroupBy(v => v.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.InstitutionId, g => g.Count, ct);

        var latestBalances = await _db.WalletLedgerEntries
            .Where(w => instIds.Contains(w.InstitutionId))
            .GroupBy(w => w.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Balance = g.OrderByDescending(x => x.CreatedAt).First().BalanceAfter })
            .ToDictionaryAsync(g => g.InstitutionId, g => g.Balance, ct);

        return institutions.Select(i => new AdminInstitutionDto
        {
            Id = i.Id,
            Name = i.Name,
            Email = i.ContactEmail ?? "",
            Status = MapInstitutionStatus(i.Status),
            WalletBalance = latestBalances.GetValueOrDefault(i.Id, 0),
            ApiCallsMtd = callCounts.GetValueOrDefault(i.Id, 0),
            JoinedDate = i.CreatedAt,
            Type = i.Type.ToString()
        }).ToList();
    }

    public async Task SuspendInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        institution.Status = InstitutionStatus.Suspended;
        institution.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReactivateInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        institution.Status = InstitutionStatus.Active;
        institution.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ApproveInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        var institution = await _db.Institutions.FindAsync(new object[] { institutionId }, ct)
            ?? throw new KeyNotFoundException("Institution not found.");

        institution.Status = InstitutionStatus.Active;
        institution.OnboardingCompleted = true;
        institution.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────── Financials ────────────────────────────

    public async Task<AdminFinancialsDto> GetFinancialsAsync(string period = "mtd", CancellationToken ct = default)
    {
        var monthStart = GetPeriodStart(period);

        // All wallet ledger entries for the period
        var entries = await _db.WalletLedgerEntries
            .Include(w => w.Institution)
            .Where(w => w.CreatedAt >= monthStart)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        var credits = entries.Where(w => w.Type == WalletTransactionType.Credit).ToList();
        var debits = entries.Where(w => w.Type == WalletTransactionType.Debit).ToList();

        var grossRevenue = debits.Sum(d => Math.Abs(d.Amount));

        // NIMC costs — sum of NimcPartnerCost * calls
        var callsInPeriod = await _db.VerificationCalls
            .Where(v => v.CreatedAt >= monthStart)
            .ToListAsync(ct);

        var globalPricing = await _db.PricingRates
            .Where(r => r.InstitutionId == null && r.IsActive)
            .ToListAsync(ct);

        var nimcPayouts = 0m;
        foreach (var call in callsInPeriod)
        {
            var rate = globalPricing.FirstOrDefault(r => r.Type == call.Type);
            nimcPayouts += rate?.NimcPartnerCost ?? 0;
        }

        // Pending top-ups — credits still awaiting confirmation
        var pendingTopUps = credits
            .Where(w => w.Description != null && w.Description.Contains("pending"))
            .Select(w => new AdminTopUpDto
            {
                Id = w.Id,
                Institution = w.Institution?.Name ?? "Unknown",
                Email = w.Institution?.ContactEmail ?? "",
                Amount = w.Amount,
                Reference = w.ReferenceId ?? "",
                Submitted = w.CreatedAt.ToString("dd MMM yyyy, HH:mm")
            })
            .ToList();

        // Transaction log
        var transactions = entries
            .Take(50)
            .Select(w => new AdminTransactionDto
            {
                Reference = w.ReferenceId ?? w.Id.ToString()[..8],
                Institution = w.Institution?.Name ?? "Unknown",
                Type = w.Type == WalletTransactionType.Credit ? "Wallet Top-Up" : "API Call",
                Amount = w.Type == WalletTransactionType.Credit ? w.Amount : -w.Amount,
                Date = w.CreatedAt.ToString("dd MMM yyyy, HH:mm")
            })
            .ToList();

        var totalCalls = callsInPeriod.Count;

        return new AdminFinancialsDto
        {
            GrossRevenue = grossRevenue,
            NimcPayouts = nimcPayouts,
            NetProfit = grossRevenue - nimcPayouts,
            MarginPct = grossRevenue > 0 ? Math.Round((grossRevenue - nimcPayouts) / grossRevenue * 100, 1) : 0,
            TotalCalls = totalCalls,
            PendingTopUps = pendingTopUps,
            Transactions = transactions
        };
    }

    // ──────────────────────────── Top-ups ────────────────────────────

    public async Task ApproveTopUpAsync(Guid topupId, CancellationToken ct = default)
    {
        var entry = await _db.WalletLedgerEntries.FindAsync(new object[] { topupId }, ct)
            ?? throw new KeyNotFoundException("Top-up entry not found.");

        if (entry.Type != WalletTransactionType.Credit)
            throw new InvalidOperationException("Entry is not a credit/top-up.");

        // Remove "pending" marker to approve
        entry.Description = entry.Description?.Replace("pending", "approved") ?? "approved";
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectTopUpAsync(Guid topupId, CancellationToken ct = default)
    {
        var entry = await _db.WalletLedgerEntries.FindAsync(new object[] { topupId }, ct)
            ?? throw new KeyNotFoundException("Top-up entry not found.");

        if (entry.Type != WalletTransactionType.Credit)
            throw new InvalidOperationException("Entry is not a credit/top-up.");

        // Remove rejected entry
        _db.WalletLedgerEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────── Pricing ────────────────────────────

    public async Task<List<AdminPricingDto>> GetPricingAsync(CancellationToken ct = default)
    {
        var globalRates = await _db.PricingRates
            .Where(r => r.InstitutionId == null && r.IsActive)
            .ToListAsync(ct);

        // Map VerificationType to display names
        return globalRates.Select(r => new AdminPricingDto
        {
            Type = r.Type switch
            {
                VerificationType.Nin => "NIN",
                VerificationType.Bvn => "BVN",
                VerificationType.Phone => "Phone",
                _ => r.Type.ToString()
            },
            InstitutionCharge = r.PricePerCall,
            NimcCost = r.NimcPartnerCost
        }).ToList();
    }

    public async Task UpdatePricingAsync(string type, UpdatePricingRequest request, CancellationToken ct = default)
    {
        var verificationType = type.ToLowerInvariant() switch
        {
            "nin" => VerificationType.Nin,
            "bvn" => VerificationType.Bvn,
            "phone" => VerificationType.Phone,
            _ => throw new ArgumentException($"Unknown verification type: {type}")
        };

        // Deactivate old global rate
        var existingRates = await _db.PricingRates
            .Where(r => r.Type == verificationType && r.InstitutionId == null && r.IsActive)
            .ToListAsync(ct);

        foreach (var rate in existingRates)
        {
            rate.IsActive = false;
            rate.EffectiveTo = DateTime.UtcNow;
            rate.UpdatedAt = DateTime.UtcNow;
        }

        // Insert new rate
        var newRate = new TruvoID.Domain.Entities.PricingRate
        {
            Type = verificationType,
            PricePerCall = request.InstitutionCharge,
            NimcPartnerCost = request.NimcCost,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

        _db.PricingRates.Add(newRate);
        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private static DateTime GetMonthStart()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime GetPeriodStart(string period) => period.ToLowerInvariant() switch
    {
        "last" => GetMonthStart().AddMonths(-1),
        "q" => GetMonthStart().AddMonths(-(GetMonthStart().Month - 1) % 3),
        "ytd" => new DateTime(GetMonthStart().Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => GetMonthStart() // mtd
    };

    private static string MapInstitutionStatus(InstitutionStatus status) => status switch
    {
        InstitutionStatus.Active => "Active",
        InstitutionStatus.Suspended => "Suspended",
        InstitutionStatus.PendingActivation => "Pending",
        InstitutionStatus.Closed => "Closed",
        _ => status.ToString()
    };
}
