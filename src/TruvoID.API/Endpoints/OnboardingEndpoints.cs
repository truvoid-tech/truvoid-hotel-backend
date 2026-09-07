using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/onboarding").RequireAuthorization();

        group.MapPut("/institution", UpdateInstitutionName);
        group.MapPut("/business", UpdateBusinessProfile);
        group.MapPost("/compliance", AcceptCompliance);
        group.MapPost("/staff", InviteStaff);
        group.MapPost("/complete", CompleteOnboarding);

        return app;
    }

    private static async Task<IResult> UpdateInstitutionName(
        HttpContext ctx,
        UpdateInstitutionNameRequest request,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Institution name is required." });

        var update = Builders<Institution>.Update
            .Set(i => i.Name, request.Name.Trim())
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        var result = await db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update);

        if (result.MatchedCount == 0)
            return Results.NotFound(new { error = "Institution not found." });

        return Results.Ok(new { message = "Institution name updated." });
    }

    private static async Task<IResult> UpdateBusinessProfile(
        HttpContext ctx,
        BusinessProfileRequest request,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var update = Builders<Institution>.Update
            .Set(i => i.LegalBusinessName, request.LegalBusinessName)
            .Set(i => i.CacRcNumber, request.CacRcNumber)
            .Set(i => i.Type, request.Type.ToString())
            .Set(i => i.Address, request.Address)
            .Set(i => i.ExpectedMonthlyVolume, request.ExpectedMonthlyVolume)
            .Set(i => i.PrimaryUseCase, request.PrimaryUseCase)
            .Set(i => i.CacCertificateUrl, request.CacCertificateUrl)
            .Set(i => i.UpdatedAt, DateTime.UtcNow)
            .Max(i => i.OnboardingStep, 2);
        var result = await db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update);

        if (result.MatchedCount == 0)
            return Results.NotFound(new { error = "Institution not found." });

        return Results.Ok(new { message = "Business profile saved." });
    }

    private static async Task<IResult> AcceptCompliance(
        HttpContext ctx,
        AcceptComplianceRequest request,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        if (!request.ResellerAcknowledged || !request.DataProcessingAgreed)
            return Results.BadRequest(new { error = "Both acknowledgments are required to continue." });

        var update = Builders<Institution>.Update
            .Set(i => i.ResellerAcknowledged, true)
            .Set(i => i.DataProcessingAgreed, true)
            .Set(i => i.ComplianceAcceptedAt, DateTime.UtcNow)
            .Set(i => i.UpdatedAt, DateTime.UtcNow)
            .Max(i => i.OnboardingStep, 4);
        var result = await db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update);

        if (result.MatchedCount == 0)
            return Results.NotFound(new { error = "Institution not found." });

        return Results.Ok(new { message = "Compliance acknowledgment recorded." });
    }

    private static async Task<IResult> InviteStaff(
        HttpContext ctx,
        InviteStaffRequest request,
        MongoDbContext db,
        INotificationService notifications,
        IConfiguration config)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName))
            return Results.BadRequest(new { error = "Full name and email are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existing is not null)
            return Results.Conflict(new { error = "A user with this email already exists." });

        var institution = await db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync();
        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var inviter = await db.Users.Find(u => u.Id == ctx.GetUserId()).FirstOrDefaultAsync();

        // FrontendModels.UserRole is the canonical shared enum (Admin = 0, Staff = 1, ReadOnly = 2).
        var role = request.Role switch
        {
            0 => "Admin",
            2 => "ReadOnly",
            _ => "Staff"
        };

        var userId = Guid.NewGuid();
        var inviteToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var newUser = new User
        {
            Id = userId,
            InstitutionId = institutionId,
            Email = email,
            FullName = request.FullName.Trim(),
            // Unusable placeholder — the invited user sets a real password via the reset-password link below.
            PasswordHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Role = role,
            IsActive = false,
            DailyCallLimit = request.DailyCallLimit > 0 ? request.DailyCallLimit : 50,
            PasswordResetToken = inviteToken,
            PasswordResetTokenExpiry = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.InsertOneAsync(newUser);

        var baseUrl = config["APP_BASE_URL"] ?? "https://gettruvoid.com";
        var inviteUrl = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(inviteToken)}";
        await notifications.SendAsync(
            email,
            newUser.FullName ?? string.Empty,
            $"You're invited to join {institution.Name} on TruvoID",
            EmailTemplates.StaffInvitation(institution.Name, inviter?.FullName ?? institution.Name, role, inviteUrl));

        return Results.Ok(new StaffInviteResponse
        {
            UserId = userId.ToString(),
            FullName = newUser.FullName ?? string.Empty,
            Email = newUser.Email,
            Role = role,
            Status = "PendingInvitation",
            DailyCallLimit = newUser.DailyCallLimit,
            CreatedAt = newUser.CreatedAt
        });
    }

    private static async Task<IResult> CompleteOnboarding(
        HttpContext ctx,
        MongoDbContext db)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var update = Builders<Institution>.Update
            .Set(i => i.OnboardingComplete, true)
            .Set(i => i.OnboardingStep, 7)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        await db.Institutions.UpdateOneAsync(i => i.Id == institutionId, update);

        return Results.Ok(new { message = "Onboarding complete." });
    }

    public record UpdateInstitutionNameRequest(string Name);

    public record BusinessProfileRequest
    {
        public string LegalBusinessName { get; init; } = "";
        public string CacRcNumber { get; init; } = "";
        public int Type { get; init; }
        public string Address { get; init; } = "";
        public string ExpectedMonthlyVolume { get; init; } = "";
        public string PrimaryUseCase { get; init; } = "";
        public string? CacCertificateUrl { get; init; }
    }

    public record AcceptComplianceRequest(bool ResellerAcknowledged, bool DataProcessingAgreed);

    public record InviteStaffRequest
    {
        public string FullName { get; init; } = "";
        public string Email { get; init; } = "";
        public int Role { get; init; }
        public int DailyCallLimit { get; init; } = 50;
    }
}

public class StaffInviteResponse
{
    public string UserId { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "";
    public string Status { get; init; } = "";
    public int DailyCallLimit { get; init; }
    public DateTime CreatedAt { get; init; }
}
