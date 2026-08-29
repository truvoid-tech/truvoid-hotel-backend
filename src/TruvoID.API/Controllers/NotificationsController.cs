using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruvoID.Core.DTOs;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Controllers;

[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationPreferenceService _prefService;

    public NotificationsController(NotificationPreferenceService prefService)
    {
        _prefService = prefService;
    }

    // ── Notification Preferences (/v1/settings/notifications) ────────────────

    [HttpGet("v1/settings/notifications")]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationPreferences(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        if (institutionId == Guid.Empty) return Unauthorized();

        var prefs = await _prefService.GetOrCreateAsync(institutionId);
        return Ok(new NotificationPreferencesDto
        {
            AlertThreshold = prefs.AlertThreshold,
            EmailAlerts = prefs.EmailAlertsEnabled,
            SmsAlerts = prefs.SmsAlertsEnabled,
            VerifyEmailResults = prefs.VerifyEmailResults
        });
    }

    [HttpPost("v1/settings/notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotificationPreferences(
        [FromBody] UpdateNotificationPreferencesRequest req,
        CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        if (institutionId == Guid.Empty) return Unauthorized();

        await _prefService.UpdateNotificationPrefsAsync(
            institutionId, req.AlertThreshold, req.EmailAlerts, req.SmsAlerts, req.VerifyEmailResults);

        return Ok(new { message = "Notification preferences updated." });
    }

    // ── Wallet Alerts (/v1/wallet/alerts) ────────────────────────────────────

    [HttpGet("v1/wallet/alerts")]
    [ProducesResponseType(typeof(WalletAlertSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWalletAlerts(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        if (institutionId == Guid.Empty) return Unauthorized();

        var prefs = await _prefService.GetOrCreateAsync(institutionId);
        return Ok(new WalletAlertSettingsDto
        {
            Threshold = prefs.AlertThreshold,
            EmailEnabled = prefs.EmailAlertsEnabled,
            SmsEnabled = prefs.SmsAlertsEnabled
        });
    }

    [HttpPost("v1/wallet/alerts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWalletAlerts(
        [FromBody] UpdateWalletAlertsRequest req,
        CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        if (institutionId == Guid.Empty) return Unauthorized();

        await _prefService.UpdateWalletAlertsAsync(institutionId, req.Threshold, req.EmailEnabled, req.SmsEnabled);
        return Ok(new { message = "Wallet alert settings saved." });
    }

    // ── Billing Contact (/v1/wallet/billing-contact) ──────────────────────────

    [HttpGet("v1/wallet/billing-contact")]
    [ProducesResponseType(typeof(BillingContactDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingContact(CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        if (institutionId == Guid.Empty) return Unauthorized();

        var prefs = await _prefService.GetOrCreateAsync(institutionId);
        return Ok(new BillingContactDto
        {
            Name = prefs.BillingContactName ?? string.Empty,
            Email = prefs.BillingContactEmail ?? string.Empty
        });
    }

    [HttpPost("v1/wallet/billing-contact")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBillingContact(
        [FromBody] UpdateBillingContactRequest req,
        CancellationToken ct)
    {
        var institutionId = GetInstitutionId();
        if (institutionId == Guid.Empty) return Unauthorized();

        await _prefService.UpdateBillingContactAsync(institutionId, req.Name, req.Email);
        return Ok(new { message = "Billing contact updated." });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private Guid GetInstitutionId()
    {
        var claim = User.FindFirst("institution_id");
        if (claim is null || !Guid.TryParse(claim.Value, out var id)) return Guid.Empty;
        return id;
    }
}
