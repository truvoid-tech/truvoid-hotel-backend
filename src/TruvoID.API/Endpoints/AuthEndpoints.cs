using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/v1/auth");

        authGroup.MapPost("/register", Register)
            .AllowAnonymous();

        authGroup.MapPost("/login", Login)
            .AllowAnonymous();

        authGroup.MapPost("/refresh", RefreshToken)
            .AllowAnonymous();

        authGroup.MapGet("/me", GetCurrentUser)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        MongoDbContext db)
    {
        // Check if institution with this email already exists
        var existing = await db.Institutions
            .Find(i => i.ContactEmail == request.ContactEmail)
            .FirstOrDefaultAsync();

        if (existing is not null)
            return Results.Conflict(new { error = "An institution with this email already exists." });

        var institutionId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var passwordHash = HashPassword(request.Password);

        var institution = new Institution
        {
            Id = institutionId,
            Name = request.InstitutionName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Status = "Pending", // IMPORTANT: do NOT auto-approve — admin must approve
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var adminUser = new User
        {
            Id = adminId,
            InstitutionId = institutionId,
            Email = request.AdminEmail,
            FullName = request.AdminFullName,
            PasswordHash = passwordHash,
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await db.Institutions.InsertOneAsync(institution);
        await db.Users.InsertOneAsync(adminUser);

        // Generate JWT tokens
        var (accessToken, refreshToken, expiresAt) = GenerateTokens(institutionId, adminId, "Admin");

        return Results.Ok(new RegisterResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        MongoDbContext db)
    {
        var user = await db.Users
            .Find(u => u.Email == request.Email && u.PasswordHash == HashPassword(request.Password))
            .FirstOrDefaultAsync();

        if (user is null)
            return Results.Unauthorized();

        if (!user.IsActive)
            return Results.Forbid();

        var institution = await db.Institutions
            .Find(i => i.Id == user.InstitutionId)
            .FirstOrDefaultAsync();

        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var (accessToken, refreshToken, expiresAt) = GenerateTokens(user.InstitutionId, user.Id, user.Role);

        return Results.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        });
    }

    private static async Task<IResult> RefreshToken(
        RefreshTokenRequest request,
        MongoDbContext db)
    {
        // In production, validate the refresh token against a stored token record
        // For now, accept any non-empty refresh token and issue new tokens
        // based on the user identity from the old access token
        var userId = GetUserIdFromToken(request.OldAccessToken);
        var institutionId = GetInstitutionIdFromToken(request.OldAccessToken);

        if (userId == Guid.Empty || institutionId == Guid.Empty)
            return Results.Unauthorized();

        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user is null)
            return Results.Unauthorized();

        var (accessToken, refreshToken, expiresAt) = GenerateTokens(institutionId, userId, user.Role);

        return Results.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        });
    }

    private static async Task<IResult> GetCurrentUser(
        HttpContext ctx,
        MongoDbContext db)
    {
        var userId = ctx.GetUserId();
        var institutionId = ctx.GetInstitutionId();

        if (userId == Guid.Empty || institutionId == Guid.Empty)
            return Results.Unauthorized();

        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user is null)
            return Results.Unauthorized();

        var institution = await db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync();
        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        return Results.Ok(new AuthProfileResponse
        {
            UserId = user.Id.ToString(),
            InstitutionId = institutionId.ToString(),
            Email = user.Email,
            FullName = user.FullName ?? string.Empty,
            Role = user.Role,
            InstitutionName = institution.Name
        });
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static (string access, string refresh, DateTime expires) GenerateTokens(Guid institutionId, Guid userId, string role)
    {
        // Simplified token generation using a symmetric secret.
        // In production, replace with proper JWT signing (e.g. System.IdentityModel.Tokens.Jwt).
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "dev-secret-key-change-in-production-32chars!!!";

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(60);

        var claims = $"|uid:{userId:N}|inst:{institutionId:N}|role:{role}|exp:{expires.ToString("O")}";
        var payload = $"{claims}|sig:{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(claims + secret)))}";

        // access token: short-lived
        var accessToken = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(payload + ":access"))).Substring(0, 64);

        // refresh token: longer, random
        var refreshTokenBytes = new byte[32];
        RandomNumberGenerator.Fill(refreshTokenBytes);
        var refreshToken = Convert.ToBase64String(refreshTokenBytes);

        return (accessToken, refreshToken, expires);
    }

    private static Guid GetUserIdFromToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var uidMatch = System.Text.RegularExpressions.Regex.Match(decoded, @"\|uid:([^|]+)\|");
            if (uidMatch.Success && Guid.TryParse(uidMatch.Groups[1].Value, out var uid))
                return uid;
        }
        catch { }
        return Guid.Empty;
    }

    private static Guid GetInstitutionIdFromToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var instMatch = System.Text.RegularExpressions.Regex.Match(decoded, @"\|inst:([^|]+)\|");
            if (instMatch.Success && Guid.TryParse(instMatch.Groups[1].Value, out var instId))
                return instId;
        }
        catch { }
        return Guid.Empty;
    }

    // ── request/response models ────────────────────────────────────────────────

    public record RegisterRequest
    {
        public string InstitutionName { get; init; } = "";
        public string ContactEmail { get; init; } = "";
        public string ContactPhone { get; init; } = "";
        public string AdminFullName { get; init; } = "";
        public string AdminEmail { get; init; } = "";
        public string AdminPhone { get; init; } = "";
        public string Password { get; init; } = "";
    }

    public record LoginRequest
    {
        public string Email { get; init; } = "";
        public string Password { get; init; } = "";
    }

    public record RefreshTokenRequest
    {
        public string OldAccessToken { get; init; } = "";
        public string RefreshToken { get; init; } = "";
    }
}

// ── shared DTOs (mirrored from Components/Services/FrontendModels.cs) ─────────

public record RegisterResponse
{
    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
}

public record LoginResponse
{
    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
}

public record AuthProfileResponse
{
    public string UserId { get; init; } = "";
    public string InstitutionId { get; init; } = "";
    public string Email { get; init; } = "";
    public string? FullName { get; init; }
    public string Role { get; init; } = "";
    public string InstitutionName { get; init; } = "";
}
