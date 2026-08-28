using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly MongoDbContext _db;

    public AdminService(MongoDbContext db) => _db = db;

    public async Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        var allInstitutions = await _db.Institutions.Find(_ => true).ToListAsync(ct);
        var activeCount = allInstitutions.Count(i => i.Status == InstitutionStatus.Active);
        var pendingCount = allInstitutions.Count(i => i.Status == InstitutionStatus.PendingActivation);
        var newThisMonth = allInstitutions.Count(i => i.CreatedAt >= monthStart);

        var callsThisMonth = await _db.VerificationCalls
            .Find(v => v.CreatedAt >= monthStart).ToListAsync(ct);

        var callsLastMonth = await _db.VerificationCalls
            .Find(v => v.CreatedAt >= lastMonthStart && v.CreatedAt < monthStart).ToListAsync(ct);

        var debitsThisMonth = await _db.WalletLedgerEntries
            .Find(w => w.Type == WalletTransactionType.Debit && w.CreatedAt >= monthStart).ToListAsync(ct);

        var debitsLastMonth = await _db.WalletLedgerEntries
            .Find(w => w.Type == WalletTransactionType.Debit && w.CreatedAt >= lastMonthStart && w.CreatedAt < monthStart).ToListAsync(ct);

        var revenueMtd = debitsThisMonth.Sum(d => Math.Abs(d.Amount));
        var revenueLastMonth = debitsLastMonth.Sum(d => Math.Abs(d.Amount));

        var globalPricing = await _db.PricingRates
            .Find(r => r.InstitutionId == null && r.IsActive).ToListAsync(ct);

        var callsByType = callsThisMonth.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
        var totalNimcCost = 0m;
        foreach (var kvp in callsByType)
        {
            var rate = globalPricing.FirstOrDefault(r => r.Type == kvp.Key);
            totalNimcCost += (rate?.NimcPartnerCost ?? 0) * kvp.Value;
        }

        // Total wallet balances — get latest entry per institution
        var totalWalletBalances = 0m;
        var instIds = allInstitutions.Select(i => i.Id).ToList();
        foreach (var instId in instIds)
        {
            var lastEntry = await _db.WalletLedgerEntries
                .Find(w => w.InstitutionId == instId)
                .SortByDescending(w => w.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (lastEntry is not null)
                totalWalletBalances += lastEntry.BalanceAfter;
        }

        var pendingTopUps = (int)await _db.WalletLedgerEntries
            .CountDocumentsAsync(Builders<WalletLedgerEntry>.Filter.And(
                Builders<WalletLedgerEntry>.Filter.Eq(w => w.Type, WalletTransactionType.Credit),
                Builders<WalletLedgerEntry>.Filter.Ne(w => w.Description, null),
                Builders<WalletLedgerEntry>.Filter.Regex(w => w.Description, new MongoDB.Bson.BsonRegularExpression("pending", "i"))), null, ct);

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
        var topInstitutions = new List<InstitutionVolumeDto>();
        foreach (var g in topInstData)
        {
            var inst = allInstitutions.FirstOrDefault(i => i.Id == g.Key);
            var callCount = g.Count();
            var revForInst = debitsThisMonth.Where(d => d.InstitutionId == g.Key).Sum(d => Math.Abs(d.Amount));
            topInstitutions.Add(new InstitutionVolumeDto
            {
                Id = g.Key,
                Name = inst?.Name ?? "Unknown",
                Email = inst?.ContactEmail ?? "",
                CallsMtd = callCount,
                RevenueMtd = revForInst,
                Active = inst?.Status == InstitutionStatus.Active
            });
        }

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
                NinCalls = callsThisMonth.Count(c => c.Type == VerificationType.Nin),
                BvnCalls = callsThisMonth.Count(c => c.Type == VerificationType.Bvn),
                PhoneCalls = callsThisMonth.Count(c => c.Type == VerificationType.Phone)
            }
        };
    }

    public async Task<List<AdminInstitutionDto>> GetInstitutionsAsync(string? search = null, string? status = null, CancellationToken ct = default)
    {
        var filterBuilder = Builders<Domain.Entities.Institution>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            filter = filterBuilder.And(filter,
                filterBuilder.Or(
                    filterBuilder.Regex(i => i.Name, new MongoDB.Bson.BsonRegularExpression(term, "i")),
                    filterBuilder.Regex(i => i.ContactEmail, new MongoDB.Bson.BsonRegularExpression(term, "i"))));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InstitutionStatus>(status, true, out var statusEnum))
            filter = filterBuilder.And(filter, filterBuilder.Eq(i => i.Status, statusEnum));

        var institutions = await _db.Institutions.Find(filter)
            .SortByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var instIds = institutions.Select(i => i.Id).ToList();
        var monthStart = GetMonthStart();

        // Get call counts and balances per institution
        var results = new List<AdminInstitutionDto>();
        foreach (var inst in institutions)
        {
            var callCount = (int)await _db.VerificationCalls.CountDocumentsAsync(
                Builders<Domain.Entities.VerificationCall>.Filter.And(Builders<Domain.Entities.VerificationCall>.Filter.Eq(v => v.InstitutionId, inst.Id), Builders<Domain.Entities.VerificationCall>.Filter.Gte(v => v.CreatedAt, monthStart)), null, ct);

            var lastEntry = await _db.WalletLedgerEntries
                .Find(w => w.InstitutionId == inst.Id)
                .SortByDescending(w => w.CreatedAt)
                .FirstOrDefaultAsync(ct);

            results.Add(new AdminInstitutionDto
            {
                Id = inst.Id,
                Name = inst.Name,
                Email = inst.ContactEmail ?? "",
                Status = MapInstitutionStatus(inst.Status),
                WalletBalance = lastEntry?.BalanceAfter ?? 0,
                ApiCallsMtd = callCount,
                JoinedDate = inst.CreatedAt,
                Type = inst.Type.ToString()
            });
        }

        return results;
    }

    public async Task SuspendInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        var update = Builders<Domain.Entities.Institution>.Update
            .Set(i => i.Status, InstitutionStatus.Suspended)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        var result = await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);
        if (result.MatchedCount == 0) throw new KeyNotFoundException("Institution not found.");
    }

    public async Task ReactivateInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        var update = Builders<Domain.Entities.Institution>.Update
            .Set(i => i.Status, InstitutionStatus.Active)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        var result = await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);
        if (result.MatchedCount == 0) throw new KeyNotFoundException("Institution not found.");
    }

    public async Task ApproveInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        var update = Builders<Domain.Entities.Institution>.Update
            .Set(i => i.Status, InstitutionStatus.Active)
            .Set(i => i.OnboardingCompleted, true)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        var result = await _db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update, cancellationToken: ct);
        if (result.MatchedCount == 0) throw new KeyNotFoundException("Institution not found.");
    }

    public async Task<AdminFinancialsDto> GetFinancialsAsync(string period = "mtd", CancellationToken ct = default)
    {
        var monthStart = GetPeriodStart(period);

        var entries = await _db.WalletLedgerEntries
            .Find(w => w.CreatedAt >= monthStart)
            .SortByDescending(w => w.CreatedAt)
            .Limit(100)
            .ToListAsync(ct);

        var credits = entries.Where(w => w.Type == WalletTransactionType.Credit).ToList();
        var debits = entries.Where(w => w.Type == WalletTransactionType.Debit).ToList();
        var grossRevenue = debits.Sum(d => Math.Abs(d.Amount));

        var callsInPeriod = await _db.VerificationCalls
            .Find(v => v.CreatedAt >= monthStart).ToListAsync(ct);

        var globalPricing = await _db.PricingRates
            .Find(r => r.InstitutionId == null && r.IsActive).ToListAsync(ct);

        var nimcPayouts = 0m;
        foreach (var call in callsInPeriod)
        {
            var rate = globalPricing.FirstOrDefault(r => r.Type == call.Type);
            nimcPayouts += rate?.NimcPartnerCost ?? 0;
        }

        // Get institution names for entries
        var instIds = entries.Select(w => w.InstitutionId).Distinct().ToList();
        var institutions = await _db.Institutions.Find(i => instIds.Contains(i.Id)).ToListAsync(ct);
        var instDict = institutions.ToDictionary(i => i.Id, i => i);

        var pendingTopUps = credits
            .Where(w => w.Description != null && w.Description.Contains("pending"))
            .Select(w => new AdminTopUpDto
            {
                Id = w.Id,
                Institution = instDict.GetValueOrDefault(w.InstitutionId)?.Name ?? "Unknown",
                Email = instDict.GetValueOrDefault(w.InstitutionId)?.ContactEmail ?? "",
                Amount = w.Amount,
                Reference = w.ReferenceId ?? "",
                Submitted = w.CreatedAt.ToString("dd MMM yyyy, HH:mm")
            }).ToList();

        var transactions = entries
            .Take(50)
            .Select(w => new AdminTransactionDto
            {
                Reference = w.ReferenceId ?? w.Id.ToString()[..8],
                Institution = instDict.GetValueOrDefault(w.InstitutionId)?.Name ?? "Unknown",
                Type = w.Type == WalletTransactionType.Credit ? "Wallet Top-Up" : "API Call",
                Amount = w.Type == WalletTransactionType.Credit ? w.Amount : -w.Amount,
                Date = w.CreatedAt.ToString("dd MMM yyyy, HH:mm")
            }).ToList();

        return new AdminFinancialsDto
        {
            GrossRevenue = grossRevenue,
            NimcPayouts = nimcPayouts,
            NetProfit = grossRevenue - nimcPayouts,
            MarginPct = grossRevenue > 0 ? Math.Round((grossRevenue - nimcPayouts) / grossRevenue * 100, 1) : 0,
            TotalCalls = callsInPeriod.Count,
            PendingTopUps = pendingTopUps,
            Transactions = transactions
        };
    }

    public async Task ApproveTopUpAsync(Guid topupId, CancellationToken ct = default)
    {
        var entry = await _db.WalletLedgerEntries.Find(e => e.Id == topupId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Top-up entry not found.");

        if (entry.Type != WalletTransactionType.Credit)
            throw new InvalidOperationException("Entry is not a credit/top-up.");

        var update = Builders<WalletLedgerEntry>.Update
            .Set(e => e.Description, entry.Description?.Replace("pending", "approved") ?? "approved")
            .Set(e => e.UpdatedAt, DateTime.UtcNow);
        await _db.WalletLedgerEntries.UpdateOneAsync(e => e.Id == topupId, update, cancellationToken: ct);
    }

    public async Task RejectTopUpAsync(Guid topupId, CancellationToken ct = default)
    {
        var entry = await _db.WalletLedgerEntries.Find(e => e.Id == topupId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Top-up entry not found.");

        if (entry.Type != WalletTransactionType.Credit)
            throw new InvalidOperationException("Entry is not a credit/top-up.");

        await _db.WalletLedgerEntries.DeleteOneAsync(e => e.Id == topupId, cancellationToken: ct);
    }

    public async Task<List<AdminPricingDto>> GetPricingAsync(CancellationToken ct = default)
    {
        var globalRates = await _db.PricingRates
            .Find(r => r.InstitutionId == null && r.IsActive)
            .ToListAsync(ct);

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
        var deactivateUpdate = Builders<PricingRate>.Update
            .Set(r => r.IsActive, false)
            .Set(r => r.EffectiveTo, DateTime.UtcNow)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);
        await _db.PricingRates.UpdateManyAsync(
            r => r.Type == verificationType && r.InstitutionId == null && r.IsActive,
            deactivateUpdate, cancellationToken: ct);

        // Insert new rate
        var newRate = new TruvoID.Domain.Entities.PricingRate
        {
            Type = verificationType,
            PricePerCall = request.InstitutionCharge,
            NimcPartnerCost = request.NimcCost,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };
        await _db.PricingRates.InsertOneAsync(newRate, cancellationToken: ct);
    }

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
        _ => GetMonthStart()
    };

    private static string MapInstitutionStatus(InstitutionStatus status) => status switch
    {
        InstitutionStatus.Active => "Active",
        InstitutionStatus.Suspended => "Suspended",
        InstitutionStatus.PendingActivation => "Pending",
        InstitutionStatus.Closed => "Closed",
        _ => status.ToString()
    };

    // ──────────────────────────── Admin Management ────────────────────────────

    public async Task<List<AdminUserDto>> GetAdminsAsync(CancellationToken ct = default)
    {
        var admins = await _db.Users
            .Find(u => u.Role == TruvoID.Domain.Enums.UserRole.Admin || u.Role == TruvoID.Domain.Enums.UserRole.PlatformAdmin)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        return admins.Select(u => new AdminUserDto
        {
            UserId = u.Id,
            Email = u.Email,
            FullName = u.FullName ?? "",
            Role = u.Role.ToString(),
            IsActive = u.Status == TruvoID.Domain.Enums.UserStatus.Active,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();
    }

    public async Task<AdminUserDto> InviteAdminAsync(InviteAdminRequest request, CancellationToken ct = default)
    {
        var exists = await _db.Users.CountDocumentsAsync(u => u.Email == request.Email, cancellationToken: ct);
        if (exists > 0)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new TruvoID.Domain.Entities.User
        {
            Email = request.Email,
            FullName = request.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = TruvoID.Domain.Enums.UserRole.Admin,
            Status = TruvoID.Domain.Enums.UserStatus.Active
        };

        await _db.Users.InsertOneAsync(user, cancellationToken: ct);

        await _db.AuditLogs.InsertOneAsync(new TruvoID.Domain.Entities.AuditLog
        {
            ActorType = "PlatformAdmin",
            Action = TruvoID.Domain.Enums.AuditAction.Created,
            Entity = "User",
            EntityId = user.Id,
            DetailsJson = "{\"email\":\"" + request.Email + "\",\"role\":\"Admin\"}"
        }, cancellationToken: ct);

        return new AdminUserDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName ?? "",
            Role = user.Role.ToString(),
            IsActive = true,
            CreatedAt = user.CreatedAt
        };
    }

    // ──────────────────────────── Audit Log ────────────────────────────

    public async Task<List<AuditLogEntryDto>> GetAuditLogAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var entries = await _db.AuditLogs
            .Find(_ => true)
            .SortByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var actorIds = entries.Where(a => a.ActorId.HasValue).Select(a => a.ActorId!.Value).Distinct().ToList();
        var actors = new Dictionary<Guid, string>();
        if (actorIds.Any())
        {
            var users = await _db.Users.Find(u => actorIds.Contains(u.Id)).ToListAsync(ct);
            actors = users.ToDictionary(u => u.Id, u => u.Email);
        }

        return entries.Select(e => new AuditLogEntryDto
        {
            Id = e.Id,
            ActorEmail = e.ActorId.HasValue && actors.ContainsKey(e.ActorId.Value) ? actors[e.ActorId.Value] : null,
            ActorType = e.ActorType,
            Action = e.Action.ToString(),
            Entity = e.Entity,
            DetailsJson = e.DetailsJson,
            CreatedAt = e.CreatedAt
        }).ToList();
    }
}
