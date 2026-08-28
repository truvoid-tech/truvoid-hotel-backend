using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class User : BaseEntity
{
    public Guid? InstitutionId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Staff;
    public UserStatus Status { get; set; } = UserStatus.PendingInvitation;
    public int? DailyCallLimit { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
}
