using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly TruvoIDDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthService(TruvoIDDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.AdminEmail, ct))
            throw new InvalidOperationException("An account with this email already exists.");

        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Name = request.InstitutionName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Type = InstitutionType.Other,
            Status = InstitutionStatus.PendingActivation,
            CreatedAt = DateTime.UtcNow
        };
        _db.Institutions.Add(institution);

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

        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync(ct);

        var (accessToken, expiresAt) = GenerateAccessToken(adminUser, institution);
        var refreshToken = await GenerateRefreshTokenAsync(adminUser.Id, ct);

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
        var user = await _db.Users
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Account is not active. Please contact your administrator.");

        if (user.InstitutionId.HasValue && user.Institution?.Status == InstitutionStatus.Suspended)
            throw new UnauthorizedAccessException("Institution account is suspended.");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var institutionForToken = user.Institution ?? new Institution
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
                InstitutionName = user.Institution?.Name ?? "TruvoID Platform"
            }
        };
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenRecord = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked, ct);

        if (tokenRecord is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (tokenRecord.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        tokenRecord.IsRevoked = true;
        tokenRecord.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Id == tokenRecord.UserId, ct);

        if (user is null || user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("User account is not active.");

        var institutionForToken = user.Institution ?? new Institution
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
        var tokenRecord = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken, ct);

        if (tokenRecord is not null)
        {
            tokenRecord.IsRevoked = true;
            tokenRecord.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null) return;

        var resetToken = GenerateSecureToken();
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync(ct);

        // TODO: Send email with reset token via Resend/SendGrid
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.PasswordResetToken == request.Token
                 && u.PasswordResetTokenExpiry > DateTime.UtcNow, ct);

        if (user is null)
            throw new UnauthorizedAccessException("Invalid or expired reset token.");

        ValidatePassword(request.NewPassword);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        ValidatePassword(request.NewPassword);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserProfile?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return null;

        return new UserProfile
        {
            UserId = user.Id,
            InstitutionId = user.InstitutionId,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            InstitutionName = user.Institution?.Name ?? "TruvoID Platform"
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
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        return token.Token;
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");
    }

    private async Task AuditLoginAsync(Guid userId, CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = userId,
            ActorType = "User",
            Action = AuditAction.Login,
            Entity = "User",
            EntityId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
