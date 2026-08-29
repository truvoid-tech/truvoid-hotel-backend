using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _email;
    private readonly MongoDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IEmailService email, MongoDbContext db, ILogger<NotificationService> logger)
    {
        _email = email;
        _db = db;
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

            var throttleUpdate = Builders<NotificationPreference>.Update
                .Set(p => p.LastLowBalanceAlertAt, DateTime.UtcNow)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);
            await _db.NotificationPreferences.UpdateOneAsync(p => p.Id == prefs.Id, throttleUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send low balance alert for institution {InstitutionId}", institutionId);
        }
    }

    public async Task SendVerificationResultAsync(Guid institutionId, string verificationType, string status, string callId, decimal cost)
    {
        try
        {
            var prefs = await _db.NotificationPreferences
                .Find(p => p.InstitutionId == institutionId)
                .FirstOrDefaultAsync();

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
}
