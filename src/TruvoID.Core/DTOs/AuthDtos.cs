using TruvoID.Domain.Enums;

namespace TruvoID.Core.DTOs;

// ─── Login ───
public record LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserProfile Profile { get; init; } = null!;
}

public record UserProfile
{
    public Guid UserId { get; init; }
    public Guid? InstitutionId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? FullName { get; init; }
    public UserRole Role { get; init; }
    public string InstitutionName { get; init; } = string.Empty;
}

// ─── Register (combined institution + admin user) ───
public record RegisterRequest
{
    public string InstitutionName { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public string AdminFullName { get; init; } = string.Empty;
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPhone { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record RegisterResponse
{
    public Guid? InstitutionId { get; init; }
    public Guid UserId { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

// ─── Token Refresh ───
public record RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public record TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

// ─── Password Reset ───
public record ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

public record ResetPasswordRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

// ─── Change Password ───
public record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
