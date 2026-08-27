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
        // Check for existing institution with same email
        if (await _db.Institutions.AnyAsync(i => i.ContactEmail == request.ContactEmail, ct))
            throw new InvalidOperationException("An institution with this email already exists.");

        // Check for existing user with same email
        if (await _db.Users.AnyAsync(u => u.Email == request.AdminEmail, ct))
            throw new InvalidOperationException("A user with this email already exists.");

        // Validate password strength
        ValidatePassword(request.Password);

        // Create institution
        var institution = new Institution
        {
            Name = request.InstitutionName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Status = InstitutionStatus.PendingActivation,
            OnboardingStep = 1
        };

        _db.Institutions.Add(institution);
        await _db.SaveChangesAsync(ct);

        // Create admin user
        var adminUser = new User
        {
            InstitutionId = institution.Id,
            Email = request.AdminEmail,
            FullName = request.AdminFullName,
            PhoneNumber = request.AdminPhone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            DailyCallLimit = null // Admin has no limit
        };

        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync(ct);

        // Generate tokens
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

        if (user.Institution.Status == InstitutionStatus.Suspended)
            throw new UnauthorizedAccessException("Institution account is suspended.");

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Generate tokens
        var (accessToken, expiresAt) = GenerateAccessToken(user, user.Institution);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, ct);

        // Audit log
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
                InstitutionName = user.Institution.Name
            }
        };
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenRecord = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked, ct);

        if (tokenRecord is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (tokenRecord.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        // Revoke the old refresh token (rotate)
        tokenRecord.IsRevoked = true;
        tokenRecord.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Get user
        var user = await _db.Users
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Id == tokenRecord.UserId, ct);

        if (user is null || user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("User account is not active.");

        // Generate new tokens
        var (accessToken, expiresAt) = GenerateAccessToken(user, user.Institution);
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
        var tokenRecord = await _db.Set<RefreshToken>()
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

        // Always return success to prevent email enumeration
        if (user is null) return;

        // Generate reset token
        var resetToken = GenerateSecureToken();
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1); // 1 hour expiry
        await _db.SaveChangesAsync(ct);

        // TODO: Send email with reset token via Resend/SendGrid
        // For now, the token is generated and stored. In production,
        // this would trigger a transactional email.
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

        // Revoke all refresh tokens for this user (force re-login)
        var tokens = await _db.Set<RefreshToken>()
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
            InstitutionName = user.Institution.Name
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

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("institution_id", user.InstitutionId.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim("institution_name", institution.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"] ?? "TruvoID",
            audience: jwtSettings["Audience"] ?? "TruvoID",
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (accessToken, expiresAt);
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7), // 7-day refresh token
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<RefreshToken>().Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return refreshToken.Token;
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");
        if (!password.Any(char.IsUpper))
            throw new ArgumentException("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one number.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Password must contain at least one special character.");
    }

    private async Task AuditLoginAsync(Guid userId, CancellationToken ct)
    {
        var log = new AuditLog
        {
            ActorId = userId,
            ActorType = "User",
            Action = AuditAction.Login,
            Entity = nameof(User),
            EntityId = userId
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}

