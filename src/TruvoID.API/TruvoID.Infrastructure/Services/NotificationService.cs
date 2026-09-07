using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public interface INotificationService
{
    Task SendWelcomeAsync(string toEmail, string adminName, string institutionName);
    Task SendApprovalAsync(string toEmail, string adminName, string institutionName);
    Task SendPasswordResetAsync(string toEmail, string adminName, string resetToken, string baseUrl);
    Task SendStaffInvitationAsync(string toEmail, string institutionName, string inviterName, string role, string inviteToken, string baseUrl);
    Task CheckAndSendLowBalanceAlertAsync(Guid institutionId, decimal newBalance);
    Task SendVerificationResultAsync(Guid institutionId, string verificationType, string status, string callId, decimal cost);
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}

public class NotificationService : INotificationService
{
    private readonly IEmailService _email;
    private readonly MongoDbContext _db;
    private readonly NotificationFeedService _feed;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IEmailService email, MongoDbContext db, NotificationFeedService feed, ILogger<NotificationService> logger)
    {
        _email = email;
        _db = db;
        _feed = feed;
        _logger = logger;
    }

    public async Task SendWelcomeAsync(string toEmail, string adminName, string institutionName)
    {
        try
        {
            await _email.SendAsync(toEmail, adminName,
                "Welcome to TruvoID — Application Received",
                EmailTemplates.Welcome(institutionName, adminName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
        }
    }

    public async Task SendApprovalAsync(string toEmail, string adminName, string institutionName)
    {
        try
        {
            await _email.SendAsync(toEmail, adminName,
                "Your TruvoID Account Is Approved",
                EmailTemplates.Approved(institutionName, adminName));

            // Push in-app notification
            var institution = await _db.Institutions
                .Find(i => i.Name == institutionName)
                .FirstOrDefaultAsync();
            if (institution is not null)
                await _feed.PushAsync(institution.Id, "approval", "Account Approved",
                    $"Your institution {institutionName} has been approved. You can now start making verifications.", "/dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send approval email to {Email}", toEmail);
        }
    }

    public async Task SendPasswordResetAsync(string toEmail, string adminName, string resetToken, string baseUrl)
    {
        var resetUrl = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        try
        {
            await _email.SendAsync(toEmail, adminName,
                "Reset Your TruvoID Password",
                EmailTemplates.PasswordReset(adminName, resetUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
        }
    }

    public async Task SendStaffInvitationAsync(string toEmail, string institutionName, string inviterName, string role, string inviteToken, string baseUrl)
    {
        var inviteUrl = $"{baseUrl.TrimEnd('/')}/accept-invite?token={Uri.EscapeDataString(inviteToken)}";
        try
        {
            await _email.SendAsync(toEmail, string.Empty,
                $"You're invited to join {institutionName} on TruvoID",
                EmailTemplates.StaffInvitation(institutionName, inviterName, role, inviteUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send staff invitation email to {Email}", toEmail);
        }
    }

    public async Task CheckAndSendLowBalanceAlertAsync(Guid institutionId, decimal newBalance)
    {
        try
        {
            var prefs = await _db.NotificationPreferences
                .Find(p => p.InstitutionId == institutionId)
                .FirstOrDefaultAsync();

            if (prefs is null || !prefs.EmailAlertsEnabled || newBalance > prefs.AlertThreshold)
                return;

            // Throttle: don't send more than once every 6 hours
            if (prefs.LastLowBalanceAlertAt.HasValue &&
                (DateTime.UtcNow - prefs.LastLowBalanceAlertAt.Value).TotalHours < 6)
                return;

            var institution = await _db.Institutions
                .Find(i => i.Id == institutionId)
                .FirstOrDefaultAsync();

            if (institution is null) return;

            var recipients = new List<(string Email, string Name)>();

            if (!string.IsNullOrWhiteSpace(prefs.BillingContactEmail))
                recipients.Add((prefs.BillingContactEmail, prefs.BillingContactName ?? "Finance"));

            if (!string.IsNullOrWhiteSpace(institution.ContactEmail))
                recipients.Add((institution.ContactEmail, institution.Name));

            foreach (var (email, name) in recipients.DistinctBy(r => r.Email))
            {
                await _email.SendAsync(email, name,
                    $"Low Balance Alert — {institution.Name}",
                    EmailTemplates.LowBalance(institution.Name, newBalance, prefs.AlertThreshold));
            }

            // Push in-app notification
            await _feed.PushAsync(institutionId, "low_balance", "Low Balance Alert",
                $"Your wallet balance has dropped to ₦{newBalance:N2}, below your threshold of ₦{prefs.AlertThreshold:N2}. Top up to avoid service interruption.", "/wallet");

            var throttleUpdate = Builders<NotificationPreference>.Update
                .Set(p => p.LastLowBalanceAlertAt, DateTime.UtcNow)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);
            await _db.NotificationPreferences.UpdateOneAsync(p => p.Id == prefs.Id, throttleUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process low balance alert for institution {InstitutionId}", institutionId);
        }
    }

    public async Task SendVerificationResultAsync(Guid institutionId, string verificationType, string status, string callId, decimal cost)
    {
        try
        {
            var prefs = await _db.NotificationPreferences
                .Find(p => p.InstitutionId == institutionId)
                .FirstOrDefaultAsync();

            // Always push in-app notification regardless of email preference
            await _feed.PushAsync(institutionId, "verification_result",
                $"Verification {status} — {verificationType.ToUpperInvariant()}",
                $"Verification for {verificationType.ToUpperInvariant()} returned {status}. Cost: ₦{cost:N2}.", "/history");

            // Send email only if the institution opted in
            if (prefs is null || !prefs.VerifyEmailResults) return;

            var institution = await _db.Institutions
                .Find(i => i.Id == institutionId)
                .FirstOrDefaultAsync();

            if (institution?.ContactEmail is null) return;

            await _email.SendAsync(institution.ContactEmail, institution.Name,
                $"Verification Complete — {verificationType.ToUpperInvariant()}",
                EmailTemplates.VerificationResult(institution.Name, verificationType, status, callId, cost));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification result email for institution {InstitutionId}", institutionId);
        }
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            await _email.SendAsync(toEmail, toName, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
