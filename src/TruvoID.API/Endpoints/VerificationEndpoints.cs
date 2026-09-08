using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.API.Auth;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Endpoints;

public static class VerificationEndpoints
{
    // The onboarding sandbox test page hardcodes this exact NIN as a "safe to try"
    // value (readonly input, pre-filled). Route it around the real IDaccess call
    // and wallet debit so every new signup doesn't get charged just to see how a
    // verification result looks.
    private const string SandboxNin = "12345678901";

    public static IEndpointRouteBuilder MapVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        // Accept either a dashboard JWT or an institution's own X-API-Key header —
        // programmatic integrations use the latter, the "Try Test Verification"
        // sandbox page uses the former.
        var group = app.MapGroup("/v1/verify")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser());

        group.MapPost("/nin", VerifyNin);
        group.MapPost("/bvn", VerifyBvn);
        group.MapPost("/phone", VerifyPhone);

        return app;
    }

    private static async Task<IResult> VerifyNin(
        HttpContext ctx,
        VerifyNinRequest request,
        IVerificationService verification,
        MongoDbContext db,
        CancellationToken ct)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        if (string.Equals(request.Nin?.Trim(), SandboxNin, StringComparison.Ordinal))
        {
            var lastLedger = await db.WalletLedgers
                .Find(l => l.InstitutionId == institutionId)
                .SortByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new VerificationResponse
            {
                Status = "match",
                CallId = $"TRV-SBX-{DateTime.UtcNow:yyyyMMddHHmmss}",
                WalletBalanceAfter = lastLedger?.BalanceAfter ?? 0m,
                Data = new VerificationData
                {
                    Name = "Johnathan Edward Doe",
                    DateOfBirth = "1985-04-12",
                    PhoneNumber = "08012345678",
                    Gender = "Male"
                }
            });
        }

        var response = await verification.VerifyAsync(
            institutionId, VerificationType.Nin, request.Nin ?? "", ctx.GetUserId(), ctx.GetApiKeyId(), ct: ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> VerifyBvn(
        HttpContext ctx,
        VerifyBvnRequest request,
        IVerificationService verification,
        CancellationToken ct)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var response = await verification.VerifyAsync(
            institutionId, VerificationType.Bvn, request.Bvn ?? "", ctx.GetUserId(), ctx.GetApiKeyId(), ct: ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> VerifyPhone(
        HttpContext ctx,
        VerifyPhoneRequest request,
        IVerificationService verification,
        CancellationToken ct)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var response = await verification.VerifyAsync(
            institutionId, VerificationType.Phone, request.PhoneNumber ?? "", ctx.GetUserId(), ctx.GetApiKeyId(), ct: ct);
        return Results.Ok(response);
    }

    public record VerifyNinRequest(string? Nin);
    public record VerifyBvnRequest(string? Bvn);
    public record VerifyPhoneRequest(string? PhoneNumber);
}
