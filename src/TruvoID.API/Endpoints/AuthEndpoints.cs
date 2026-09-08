using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
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

        // Platform-level accounts (e.g. PlatformAdmin) aren't scoped to an institution.
        if (user.InstitutionId != Guid.Empty)
        {
            var institution = await db.Institutions
                .Find(i => i.Id == user.InstitutionId)
                .FirstOrDefaultAsync();

            if (institution is null)
                return Results.NotFound(new { error = "Institution not found." });
        }

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
        // Validate the old access token and issue new tokens.
        // institutionId == Guid.Empty is valid for platform-level accounts — only a
        // missing/invalid userId means the token itself didn't parse.
        var (userId, institutionId, role) = GetClaimsFromToken(request.OldAccessToken);

        if (userId == Guid.Empty)
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
        // Extract claims from the JWT in the Authorization header
        var authHeader = ctx.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();

        var token = authHeader["Bearer ".Length..].Trim();
        var (userId, institutionId, _) = GetClaimsFromToken(token);

        if (userId == Guid.Empty)
            return Results.Unauthorized();

        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user is null)
            return Results.Unauthorized();

        // Platform-level accounts (e.g. PlatformAdmin) aren't scoped to an institution.
        var institutionName = string.Empty;
        if (institutionId != Guid.Empty)
        {
            var institution = await db.Institutions.Find(i => i.Id == institutionId).FirstOrDefaultAsync();
            if (institution is null)
                return Results.NotFound(new { error = "Institution not found." });
            institutionName = institution.Name;
        }

        return Results.Ok(new AuthProfileResponse
        {
            UserId = user.Id.ToString(),
            InstitutionId = institutionId.ToString(),
            Email = user.Email,
            FullName = user.FullName ?? string.Empty,
            Role = user.Role,
            InstitutionName = institutionName
        });
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    internal static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Railway sets Jwt__SecretKey / Jwt__Issuer / Jwt__Audience / Jwt__ExpiryMinutes
    // (JWT_SECRET is kept as a fallback name). Must match what Program.cs reads for
    // token validation, or tokens issued here never validate.
    private static string JwtSecret =>
        Environment.GetEnvironmentVariable("Jwt__SecretKey")
        ?? Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? "dev-secret-key-change-in-production-32chars!!!";

    private static string JwtIssuer => Environment.GetEnvironmentVariable("Jwt__Issuer") ?? "TruvoID";
    private static string JwtAudience => Environment.GetEnvironmentVariable("Jwt__Audience") ?? "TruvoID";
    private static int JwtExpiryMinutes =>
        int.TryParse(Environment.GetEnvironmentVariable("Jwt__ExpiryMinutes"), out var m) ? m : 60;

    private static (string access, string refresh, DateTime expires) GenerateTokens(Guid institutionId, Guid userId, string role)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(JwtExpiryMinutes);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("institution_id", institutionId.ToString()),
            new Claim("role", role),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now, DateTimeOffset.Now.Offset).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh token: cryptographically random, stored server-side in production
        var refreshTokenBytes = new byte[32];
        RandomNumberGenerator.Fill(refreshTokenBytes);
        var refreshToken = Convert.ToBase64String(refreshTokenBytes);

        return (accessToken, refreshToken, expiresAt);
    }

    private static (Guid userId, Guid institutionId, string role) GetClaimsFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = JwtIssuer,
                ValidateAudience = true,
                ValidAudience = JwtAudience,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var userId = jwtToken.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub)
                ? Guid.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? Guid.Empty.ToString())
                : Guid.Empty;

            var institutionId = jwtToken.Claims.Any(c => c.Type == "institution_id")
                ? Guid.Parse(principal.FindFirst("institution_id")?.Value ?? Guid.Empty.ToString())
                : Guid.Empty;

            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

            return (userId, institutionId, role);
        }
        catch { }
        return (Guid.Empty, Guid.Empty, "");
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
