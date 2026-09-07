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

public static class PasswordResetEndpoints
{
    public static IEndpointRouteBuilder MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/forgot-password", ForgotPassword).AllowAnonymous();
        app.MapPost("/v1/auth/reset-password", ResetPassword).AllowAnonymous();

        return app;
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest req,
        MongoDbContext db,
        INotificationService notifications,
        IConfiguration config)
    {
        // Always return 200 so we don't leak whether an email exists
        if (string.IsNullOrWhiteSpace(req.Email))
            return Results.Ok(new { message = "If that email is registered, a reset link has been sent." });

        var user = await db.Users
            .Find(u => u.Email == req.Email.ToLowerInvariant().Trim())
            .FirstOrDefaultAsync();

        if (user is null)
            return Results.Ok(new { message = "If that email is registered, a reset link has been sent." });

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiry = DateTime.UtcNow.AddHours(1);

        var update = Builders<User>.Update
            .Set(u => u.PasswordResetToken, token)
            .Set(u => u.PasswordResetTokenExpiry, expiry)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, update);

        var baseUrl = config["APP_BASE_URL"] ?? "https://gettruvoid.com";
        await notifications.SendPasswordResetAsync(user.Email, user.FullName ?? "Admin", token, baseUrl);

        return Results.Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest req,
        MongoDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return Results.BadRequest(new { error = "Token and new password are required." });

        var user = await db.Users
            .Find(u => u.PasswordResetToken == req.Token)
            .FirstOrDefaultAsync();

        if (user is null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            return Results.BadRequest(new { error = "Reset link is invalid or has expired." });

        var newHash = AuthEndpoints.HashPassword(req.NewPassword);

        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, newHash)
            .Set(u => u.PasswordResetToken, null)
            .Set(u => u.PasswordResetTokenExpiry, null)
            // Also activates staff accounts created via the onboarding invite flow,
            // which start inactive until the invited user sets a real password here.
            .Set(u => u.IsActive, true)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, update);

        return Results.Ok(new { message = "Password reset successfully. You can now log in." });
    }
}

public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
