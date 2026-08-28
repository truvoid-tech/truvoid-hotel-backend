using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Dashboard login identity. Belongs to an Institution.
/// </summary>
public class User : BaseEntity
{
    public Guid? InstitutionId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Staff;
    public UserStatus Status { get; set; } = UserStatus.PendingInvitation;
    
    // Per-staff daily call limits (admin-configurable)
    public int? DailyCallLimit { get; set; }
    
    // Auth tracking
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    
    // Navigation properties
    public Institution? Institution { get; set; }
    public ICollection<VerificationCall> VerificationCalls { get; set; } = new List<VerificationCall>();
}
