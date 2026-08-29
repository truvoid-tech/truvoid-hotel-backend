namespace TruvoID.Core.DTOs;

// ── Notification Settings ─────────────────────────────────────────────────────

public class NotificationPreferencesDto
{
    public decimal AlertThreshold { get; set; } = 10000;
    public bool EmailAlerts { get; set; } = true;
    public bool SmsAlerts { get; set; } = false;
    public bool VerifyEmailResults { get; set; } = false;
}

public class UpdateNotificationPreferencesRequest
{
    public decimal AlertThreshold { get; set; }
    public bool EmailAlerts { get; set; }
    public bool SmsAlerts { get; set; }
    public bool VerifyEmailResults { get; set; }
}

// ── Wallet Alerts ─────────────────────────────────────────────────────────────

public class WalletAlertSettingsDto
{
    public decimal Threshold { get; set; } = 10000;
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
}

public class UpdateWalletAlertsRequest
{
    public decimal Threshold { get; set; }
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
}

// ── Billing Contact ───────────────────────────────────────────────────────────

public class BillingContactDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UpdateBillingContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
