using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Endpoints;

public static class InstitutionEndpoints
{
    public static IEndpointRouteBuilder MapInstitutionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/institution/profile", GetProfile)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetProfile(
        HttpContext ctx,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var institution = await db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync();
        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var lastLedger = await db.WalletLedgers
            .Find(l => l.InstitutionId == institutionId)
            .SortByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();
        var walletBalance = lastLedger?.BalanceAfter ?? 0m;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var callsMtd = await db.VerificationCalls
            .Find(c => c.InstitutionId == institutionId && c.CreatedAt >= monthStart)
            .ToListAsync();

        var successfulCalls = callsMtd.Count(c => c.Status == VerificationStatus.Match);
        var failedCalls = callsMtd.Count(c => c.Status == VerificationStatus.Error);
        var spentThisMonth = callsMtd.Sum(c => c.AmountCharged);

        return Results.Ok(new InstitutionProfileResponse
        {
            Name = institution.Name,
            Status = institution.Status,
            OnboardingComplete = institution.OnboardingComplete,
            WalletBalance = walletBalance,
            ApiCallsMtd = callsMtd.Count,
            SpentThisMonth = spentThisMonth,
            SuccessfulCallsMtd = successfulCalls,
            FailedCallsMtd = failedCalls
        });
    }
}

public class InstitutionProfileResponse
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending";
    public bool OnboardingComplete { get; init; }
    public decimal WalletBalance { get; init; }
    public int ApiCallsMtd { get; init; }
    public decimal SpentThisMonth { get; init; }
    public int SuccessfulCallsMtd { get; init; }
    public int FailedCallsMtd { get; init; }
}
