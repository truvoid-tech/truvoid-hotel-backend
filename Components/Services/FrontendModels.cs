namespace TruvoID.Components.Services;

// ── Auth ────────────────────────────────────────────────────────────────────

public class RegisterResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

// Mirrors TruvoID.Domain.Enums.UserRole on the backend — NOT the same as the
// unrelated team-management UserRole enum below, whose ordinals differ.
public enum AuthRole { Staff = 0, Admin = 1, SuperAdmin = 2, PlatformAdmin = 3 }

/// <summary>Result of GET /v1/auth/me — used to verify a session is still valid server-side.</summary>
public class AuthProfile
{
    public string UserId { get; set; } = string.Empty;
    public string? InstitutionId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public AuthRole Role { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
}

// ── Verification ─────────────────────────────────────────────────────────────

public enum VerificationType { Nin, Bvn, Phone }

public class VerificationResponse
{
    public string Status { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public decimal WalletBalanceAfter { get; set; }
    public VerificationData? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

public class VerificationData
{
    public string? Name { get; set; }
    public string? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Gender { get; set; }
    public string? PhotoUrl { get; set; }
    public string? StateOfOrigin { get; set; }
    public string? ResidentialAddress { get; set; }
    public string? IdentityHash { get; set; }
    public decimal? ConfidenceScore { get; set; }
}

// ── API Keys ─────────────────────────────────────────────────────────────────

public class ApiKeyResponse
{
    public string Id { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ApiKeyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RawKey { get; set; }
}

public enum ApiKeyStatus { Active, Revoked }

public class CreateApiKeyRequest
{
    public string Description { get; set; } = string.Empty;
}

// ── Wallet ───────────────────────────────────────────────────────────────────

public class WalletBalanceResponse
{
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
}

public class WalletTransactionResponse
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public WalletTransactionType Type { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum WalletTransactionType { Credit, Debit }

// ── Per-Institution Pricing ──────────────────────────────────────────────────

public class InstitutionPricingDto
{
    public string InstitutionId { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public decimal NinPrice { get; set; }
    public decimal BvnPrice { get; set; }
    public decimal PhonePrice { get; set; }
    public decimal NinCost { get; set; }
    public decimal BvnCost { get; set; }
    public decimal PhoneCost { get; set; }
}

public class InstitutionPricingRateDto
{
    public string Type { get; set; } = string.Empty;
    public decimal PricePerCall { get; set; }
}

// ── Notification Feed ──────────────────────────────────────────────────────

public class NotificationFeedItem
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

// ── Team ─────────────────────────────────────────────────────────────────────

public enum UserRole { Admin, Staff, ReadOnly }

public enum UserStatus { Active, PendingInvitation, Suspended }

public class StaffInviteResponse
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    public int DailyCallLimit { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StaffInviteRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int DailyCallLimit { get; set; }
}

// ── History ──────────────────────────────────────────────────────────────────

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public enum VerificationStatus { Pending, Match, NoMatch, Error }

public class CallHistoryResponse
{
    public string Id { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string? ApiKeyId { get; set; }
    public VerificationType Type { get; set; }
    public VerificationStatus Status { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Document Upload ──────────────────────────────────────────────────────────

public class UploadDocumentResponse
{
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

// ── Institution Profile ──────────────────────────────────────────────────────

public class InstitutionProfileResponse
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Active, Suspended
    public bool OnboardingComplete { get; set; }
    public decimal WalletBalance { get; set; }
    public int ApiCallsMtd { get; set; }
    public decimal SpentThisMonth { get; set; }
    public int SuccessfulCallsMtd { get; set; }
    public int FailedCallsMtd { get; set; }
}

// ── Wallet Top-Up ────────────────────────────────────────────────────────────

public class TopupInitiateResponse
{
    public string? AuthorizationUrl { get; set; }
    public string? Reference { get; set; }
}

// ── Settings ─────────────────────────────────────────────────────────────────

public class OnboardingStatusResponse
{
    public InstitutionInfo? Institution { get; set; }
    public bool IsComplete { get; set; }
}

public class InstitutionInfo
{
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}

public class InstitutionSetupRequest
{
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}

// ── Notification Preferences ─────────────────────────────────────────────────

public class NotificationPreferencesResponse
{
    public decimal AlertThreshold { get; set; } = 10000;
    public bool EmailAlerts { get; set; } = true;
    public bool SmsAlerts { get; set; } = false;
    public bool VerifyEmailResults { get; set; } = false;
}

// ── Wallet Alert Settings ─────────────────────────────────────────────────────

public class WalletAlertSettingsResponse
{
    public decimal Threshold { get; set; } = 10000;
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
}

public class BillingContactResponse
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

