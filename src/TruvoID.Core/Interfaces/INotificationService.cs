namespace TruvoID.Core.Interfaces;

public interface INotificationService
{
    Task SendWelcomeAsync(string toEmail, string adminName, string institutionName);
    Task SendApprovalAsync(string toEmail, string adminName, string institutionName);
    Task SendPasswordResetAsync(string toEmail, string adminName, string resetToken, string baseUrl);
    Task SendStaffInvitationAsync(string toEmail, string institutionName, string inviterName, string role, string inviteToken, string baseUrl);
    Task CheckAndSendLowBalanceAlertAsync(Guid institutionId, decimal newBalance);
    Task SendVerificationResultAsync(Guid institutionId, string verificationType, string status, string callId, decimal cost);
}
