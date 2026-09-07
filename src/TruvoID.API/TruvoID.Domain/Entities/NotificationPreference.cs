namespace TruvoID.Domain.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public decimal AlertThreshold { get; set; } = 10000m;
    public bool EmailAlertsEnabled { get; set; } = true;
    public bool SmsAlertsEnabled { get; set; } = false;
    public bool VerifyEmailResults { get; set; } = false;
    public string? BillingContactName { get; set; }
    public string? BillingContactEmail { get; set; }
    public DateTime? LastLowBalanceAlertAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
