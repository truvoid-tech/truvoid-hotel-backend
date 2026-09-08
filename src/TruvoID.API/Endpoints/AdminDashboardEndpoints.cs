using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Endpoints;

public static class AdminDashboardEndpoints
{
    public static IEndpointRouteBuilder MapAdminDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin").RequireAuthorization("TruvoAdmin");

        group.MapGet("/overview", GetOverview);
        group.MapGet("/institutions", GetInstitutions);
        group.MapGet("/admins", GetAdmins);
        group.MapPost("/admins/invite", InviteAdmin);
        group.MapPut("/admins/{userId:guid}/role", UpdateAdminRole);

        group.MapGet("/api-keys", GetAllApiKeys);
        group.MapPost("/api-keys/{id:guid}/revoke", RevokeApiKey);

        return app;
    }

    private static async Task<IResult> GetAllApiKeys(MongoDbContext db)
    {
        var keys = await db.ApiKeys.Find(FilterDefinition<ApiKey>.Empty)
            .SortByDescending(k => k.CreatedAt)
            .ToListAsync();
        var institutions = await db.Institutions.Find(FilterDefinition<Institution>.Empty).ToListAsync();
        var institutionNames = institutions.ToDictionary(i => i.Id, i => i.Name);

        var result = keys.Select(k => new AdminApiKeyDto
        {
            Id = k.Id,
            InstitutionName = institutionNames.GetValueOrDefault(k.InstitutionId, "Unknown"),
            KeyPrefix = k.KeyPrefix,
            Description = k.Description,
            Status = k.Status,
            CallCount = k.CallCount,
            CreatedAt = k.CreatedAt,
            LastUsedAt = k.LastUsedAt
        }).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> RevokeApiKey(Guid id, MongoDbContext db)
    {
        var update = Builders<ApiKey>.Update
            .Set(k => k.Status, "Revoked")
            .Set(k => k.RevokedAt, DateTime.UtcNow);
        var result = await db.ApiKeys.UpdateOneAsync(k => k.Id == id, update);

        if (result.MatchedCount == 0)
            return Results.NotFound(new { error = "API key not found." });

        return Results.Ok(new { message = "API key revoked." });
    }

    private static async Task<IResult> GetOverview(MongoDbContext db)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        var institutions = await db.Institutions.Find(FilterDefinition<Institution>.Empty).ToListAsync();
        var allLedgers = await db.WalletLedgers
            .Find(l => l.CreatedAt >= lastMonthStart)
            .ToListAsync();
        var callsMtd = await db.VerificationCalls
            .Find(c => c.CreatedAt >= monthStart)
            .ToListAsync();
        var pendingTopUps = await db.WalletTopUps.CountDocumentsAsync(t => t.Status == "Pending");

        var ledgersMtd = allLedgers.Where(l => l.CreatedAt >= monthStart).ToList();
        var ledgersLastMonth = allLedgers.Where(l => l.CreatedAt < monthStart).ToList();

        var revenueMtd = ledgersMtd.Where(l => l.Type == "Debit").Sum(l => l.Amount);
        var costsMtd = ledgersMtd.Where(l => l.Type == "Credit").Sum(l => l.Amount);
        var revenueLastMonth = ledgersLastMonth.Where(l => l.Type == "Debit").Sum(l => l.Amount);
        var revenueGrowthPct = revenueLastMonth > 0
            ? Math.Round((revenueMtd - revenueLastMonth) / revenueLastMonth * 100, 1)
            : 0;

        // Latest balance per institution = most recent ledger entry overall (not just this month).
        var latestBalanceByInstitution = await GetLatestBalancesAsync(db, institutions.Select(i => i.Id));
        var totalWalletBalances = latestBalanceByInstitution.Values.Sum();

        var topInstitutions = institutions
            .Select(i =>
            {
                var calls = callsMtd.Where(c => c.InstitutionId == i.Id).ToList();
                return new InstitutionVolumeDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Email = i.ContactEmail ?? "",
                    CallsMtd = calls.Count,
                    RevenueMtd = calls.Sum(c => c.AmountCharged),
                    Active = i.Status == "Active"
                };
            })
            .OrderByDescending(i => i.RevenueMtd)
            .Take(5)
            .ToList();

        var overview = new AdminOverviewDto
        {
            RevenueMtd = revenueMtd,
            CostsMtd = costsMtd,
            NetMargin = revenueMtd - costsMtd,
            ActiveInstitutions = institutions.Count(i => i.Status == "Active"),
            PendingInstitutions = institutions.Count(i => i.Status == "Pending"),
            TotalApiCallsMtd = callsMtd.Count,
            TotalWalletBalances = totalWalletBalances,
            PendingTopUpApprovals = (int)pendingTopUps,
            RevenueGrowthPct = revenueGrowthPct,
            NewInstitutionsThisMonth = institutions.Count(i => i.CreatedAt >= monthStart),
            TopInstitutions = topInstitutions,
            CallBreakdown = new CallBreakdownDto
            {
                NinCalls = callsMtd.Count(c => c.Type == VerificationType.Nin),
                BvnCalls = callsMtd.Count(c => c.Type == VerificationType.Bvn),
                PhoneCalls = callsMtd.Count(c => c.Type == VerificationType.Phone)
            }
        };

        return Results.Ok(overview);
    }

    private static async Task<IResult> GetInstitutions(MongoDbContext db)
    {
        var institutions = await db.Institutions.Find(FilterDefinition<Institution>.Empty).ToListAsync();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var callsMtd = await db.VerificationCalls.Find(c => c.CreatedAt >= monthStart).ToListAsync();
        var latestBalanceByInstitution = await GetLatestBalancesAsync(db, institutions.Select(i => i.Id));

        var result = institutions.Select(i =>
        {
            var balance = latestBalanceByInstitution.GetValueOrDefault(i.Id, 0m);
            return new AdminInstitutionDto
            {
                Id = i.Id,
                Name = i.Name,
                Email = i.ContactEmail ?? "",
                Status = i.Status,
                WalletBalance = balance,
                Tokens = Math.Round(balance / 10m, 2), // ₦10 = 1 token display convention
                ApiCallsMtd = callsMtd.Count(c => c.InstitutionId == i.Id),
                JoinedDate = i.CreatedAt,
                Type = i.Type ?? ""
            };
        }).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAdmins(MongoDbContext db)
    {
        var users = await db.Users.Find(FilterDefinition<User>.Empty).ToListAsync();

        var result = users
            .OrderByDescending(u => u.Role == "PlatformAdmin")
            .ThenBy(u => u.FullName)
            .Select(u => new AdminUserDto
            {
                UserId = u.Id,
                Email = u.Email,
                FullName = u.FullName ?? "",
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> InviteAdmin(
        InviteAdminRequest request,
        MongoDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "Email, full name, and password are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existing is not null)
            return Results.Conflict(new { error = "A user with this email already exists." });

        var newAdmin = new User
        {
            Id = Guid.NewGuid(),
            InstitutionId = Guid.Empty, // platform-level account, not scoped to an institution
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = AuthEndpoints.HashPassword(request.Password),
            Role = "PlatformAdmin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.InsertOneAsync(newAdmin);

        return Results.Ok(new { message = "Platform admin invited." });
    }

    private static async Task<IResult> UpdateAdminRole(
        Guid userId,
        UpdateRoleRequest request,
        MongoDbContext db)
    {
        if (request.Role != "Admin" && request.Role != "Staff")
            return Results.BadRequest(new { error = "Role must be 'Admin' or 'Staff'." });

        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user is null)
            return Results.NotFound(new { error = "User not found." });

        if (user.Role == "PlatformAdmin")
            return Results.BadRequest(new { error = "Platform admin role cannot be changed here." });

        var update = Builders<User>.Update
            .Set(u => u.Role, request.Role)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        await db.Users.UpdateOneAsync(u => u.Id == userId, update);

        return Results.Ok(new { message = "Role updated." });
    }

    private static async Task<Dictionary<Guid, decimal>> GetLatestBalancesAsync(MongoDbContext db, IEnumerable<Guid> institutionIds)
    {
        var result = new Dictionary<Guid, decimal>();
        foreach (var id in institutionIds)
        {
            var last = await db.WalletLedgers
                .Find(l => l.InstitutionId == id)
                .SortByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();
            result[id] = last?.BalanceAfter ?? 0m;
        }
        return result;
    }
}
