using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notifications;

    public AuthService(MongoDbContext db, IConfiguration configuration, INotificationService notifications)
    {
        _db = db;
        _configuration = configuration;
        _notifications = notifications;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.CountDocumentsAsync(u => u.Email == request.AdminEmail, cancellationToken: ct) > 0)
            throw new InvalidOperationException("An account with this email already exists.");

        var institution = new Institution
        {
            Name = request.InstitutionName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Status = InstitutionStatus.PendingActivation,
            OnboardingStep = 1
        };
        await _db.Institutions.InsertOneAsync(institution, cancellationToken: ct);

        var adminUser = new User
        {
            InstitutionId = institution.Id,
            Email = request.AdminEmail,
            FullName = request.AdminFullName,
            PhoneNumber = request.AdminPhone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            DailyCallLimit = null
        };
        await _db.Users.InsertOneAsync(adminUser, cancellationToken: ct);

        var (accessToken, expiresAt) = GenerateAccessToken(adminUser, institution);
        var refreshToken = await GenerateRefreshTokenAsync(adminUser.Id, ct);

        _ = Task.Run(async () =>
            await _notifications.SendWelcomeAsync(adminUser.Email, adminUser.FullName ?? "Admin", institution.Name));

        return new RegisterResponse
        {
            InstitutionId = institution.Id,
            UserId = adminUser.Id,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.Find(u => u.Email == request.Email).FirstOrDefaultAsync(ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Account is not active. Please contact your administrator.");

        // Only check institution suspension for non-platform-admin users
        Institution? institution = null;
        if (user.InstitutionId.HasValue)
        {
            institution = await _db.Institutions.Find(i => i.Id == user.InstitutionId.Value).FirstOrDefaultAsync(ct);
            if (institution?.Status == InstitutionStatus.Suspended)
                throw new UnauthorizedAccessException("Institution account is suspended.");
        }

        // Update last login
        var update = Builders<User>.Update.Set(u => u.LastLoginAt, DateTime.UtcNow);
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, update, cancellationToken: ct);

        var institutionForToken = institution ?? new Institution
        {
            Id = Guid.Empty,
            Name = "TruvoID Platform",
            Type = InstitutionType.Other,
            Status = InstitutionStatus.Active
        };

        var (accessToken, expiresAt) = GenerateAccessToken(user, institutionForToken);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, ct);

        await AuditLoginAsync(user.Id, ct);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            Profile = new UserProfile
            {
                UserId = user.Id,
                InstitutionId = user.InstitutionId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                InstitutionName = institution?.Name ?? "TruvoID Platform"
            }
        };
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenRecord = await _db.RefreshTokens
            .Find(t => t.Token == refreshToken && !t.IsRevoked)
            .FirstOrDefaultAsync(ct);

        if (tokenRecord is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (tokenRecord.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        // Revoke old token
        var revokeUpdate = Builders<RefreshToken>.Update
            .Set(t => t.IsRevoked, true)
            .Set(t => t.RevokedAt, DateTime.UtcNow);
        await _db.RefreshTokens.UpdateOneAsync(t => t.Id == tokenRecord.Id, revokeUpdate, cancellationToken: ct);

        var user = await _db.Users.Find(u => u.Id == tokenRecord.UserId).FirstOrDefaultAsync(ct);
        if (user is null || user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("User account is not active.");

        Institution? institution = null;
        if (user.InstitutionId.HasValue)
            institution = await _db.Institutions.Find(i => i.Id == user.InstitutionId.Value).FirstOrDefaultAsync(ct);

        var institutionForToken = institution ?? new Institution
        {
            Id = Guid.Empty,
            Name = "TruvoID Platform",
            Type = InstitutionType.Other,
            Status = InstitutionStatus.Active
        };

        var (accessToken, expiresAt) = GenerateAccessToken(user, institutionForToken);
        var newRefreshToken = await GenerateRefreshTokenAsync(user.Id, ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var update = Builders<RefreshToken>.Update
            .Set(t => t.IsRevoked, true)
            .Set(t => t.RevokedAt, DateTime.UtcNow);
        await _db.RefreshTokens.UpdateOneAsync(t => t.Token == refreshToken, update, cancellationToken: ct);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
        if (user is null) return;

        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var update = Builders<User>.Update
            .Set(u => u.PasswordResetToken, resetToken)
            .Set(u => u.PasswordResetTokenExpiry, DateTime.UtcNow.AddHours(1));
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, update, cancellationToken: ct);

        var baseUrl = _configuration["APP_BASE_URL"]
            ?? Environment.GetEnvironmentVariable("APP_BASE_URL")
            ?? "https://gettruvoid.com";
        _ = Task.Run(async () =>
            await _notifications.SendPasswordResetAsync(user.Email, user.FullName ?? "Admin", resetToken, baseUrl));
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.Find(u =>
            u.PasswordResetToken == request.Token
            && u.PasswordResetTokenExpiry > DateTime.UtcNow).FirstOrDefaultAsync(ct);

        if (user is null)
            throw new UnauthorizedAccessException("Invalid or expired reset token.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");

        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(request.NewPassword))
            .Set(u => u.PasswordResetToken, (string?)null)
            .Set(u => u.PasswordResetTokenExpiry, (DateTime?)null)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        await _db.Users.UpdateOneAsync(u => u.Id == user.Id, update, cancellationToken: ct);

        // Revoke all refresh tokens
        var revokeUpdate = Builders<RefreshToken>.Update
            .Set(t => t.IsRevoked, true)
            .Set(t => t.RevokedAt, DateTime.UtcNow);
        await _db.RefreshTokens.UpdateManyAsync(t => t.UserId == user.Id && !t.IsRevoked, revokeUpdate, cancellationToken: ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");

        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(request.NewPassword))
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        await _db.Users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: ct);
    }

    public async Task<UserProfile?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
        if (user is null) return null;

        string institutionName = "TruvoID Platform";
        if (user.InstitutionId.HasValue)
        {
            var institution = await _db.Institutions.Find(i => i.Id == user.InstitutionId.Value).FirstOrDefaultAsync(ct);
            institutionName = institution?.Name ?? "Unknown";
        }

        return new UserProfile
        {
            UserId = user.Id,
            InstitutionId = user.InstitutionId,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            InstitutionName = institutionName
        };
    }

    // ─── Private Helpers ───

    private (string accessToken, DateTime expiresAt) GenerateAccessToken(User user, Institution institution)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("institution_id", user.InstitutionId?.ToString() ?? Guid.Empty.ToString()),
            new("role", user.Role.ToString()),
            new("institution_name", institution.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _db.RefreshTokens.InsertOneAsync(token, cancellationToken: ct);
        return token.Token;
    }

    private async Task AuditLoginAsync(Guid userId, CancellationToken ct)
    {
        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = userId,
            ActorType = "User",
            Action = AuditAction.Login,
            Entity = "User",
            EntityId = userId
        }, cancellationToken: ct);
    }
}
