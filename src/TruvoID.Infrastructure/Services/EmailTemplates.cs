namespace TruvoID.Infrastructure.Services;

public static class EmailTemplates
{
    private static string Wrap(string title, string body) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{title}</title></head>
        <body style="margin:0;padding:0;background:#f3f4f6;font-family:system-ui,-apple-system,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:40px 16px;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#fafafa;border-radius:8px;overflow:hidden;border:1px solid #e5e7eb;">
                <!-- Header -->
                <tr><td style="background:#1f3864;padding:28px 40px;">
                  <table width="100%" cellpadding="0" cellspacing="0">
                    <tr>
                      <td><span style="color:#fafafa;font-size:20px;font-weight:700;letter-spacing:-0.5px;">TruvoID</span></td>
                      <td align="right"><span style="color:#f5a623;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:2px;">Identity Infrastructure</span></td>
                    </tr>
                  </table>
                </td></tr>
                <!-- Gold bar -->
                <tr><td style="height:3px;background:#f5a623;"></td></tr>
                <!-- Body -->
                <tr><td style="padding:40px;">{body}</td></tr>
                <!-- Footer -->
                <tr><td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:24px 40px;text-align:center;">
                  <p style="margin:0;color:#9ca3af;font-size:12px;line-height:1.6;">
                    TruvoID &middot; Institutional Identity Verification Platform<br>
                    This is an automated notification &mdash; please do not reply to this email.
                  </p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    public static string Welcome(string institutionName, string adminName) => Wrap("Welcome to TruvoID", $"""
        <h1 style="margin:0 0 8px;font-size:24px;color:#1f3864;font-weight:700;">Welcome, {adminName}</h1>
        <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;">Your institution <strong style="color:#1f3864;">{institutionName}</strong> has been successfully registered on TruvoID. Our compliance team is now reviewing your application.</p>
        <div style="background:#fffbeb;border:1px solid #fde68a;border-radius:6px;padding:16px 20px;margin:0 0 24px;">
          <p style="margin:0;color:#92400e;font-size:14px;"><strong>What happens next:</strong> Document review typically takes 1&ndash;2 business days. You'll receive an email when your account is approved and ready for live verifications.</p>
        </div>
        <p style="margin:0;color:#6b7280;font-size:14px;">Questions? Contact us at <a href="mailto:support@truvoid.com" style="color:#1f3864;">support@truvoid.com</a></p>
        """);

    public static string Approved(string institutionName, string adminName) => Wrap("Account Approved — TruvoID", $"""
        <div style="text-align:center;margin-bottom:32px;">
          <div style="display:inline-block;background:#d1fae5;border:1px solid #6ee7b7;border-radius:50%;width:64px;height:64px;line-height:64px;font-size:28px;">&#10003;</div>
        </div>
        <h1 style="margin:0 0 8px;font-size:24px;color:#1f3864;font-weight:700;text-align:center;">You're Approved!</h1>
        <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;text-align:center;"><strong style="color:#1f3864;">{institutionName}</strong> has been verified and activated. You now have full access to TruvoID's identity verification infrastructure.</p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
          <tr><td align="center">
            <a href="https://app.truvoid.com/dashboard" style="display:inline-block;background:#1f3864;color:#fafafa;text-decoration:none;font-size:14px;font-weight:600;text-transform:uppercase;letter-spacing:1px;padding:14px 32px;border-radius:6px;">Go to Dashboard</a>
          </td></tr>
        </table>
        <p style="margin:0;color:#6b7280;font-size:13px;text-align:center;">Log in with your registered admin credentials to begin running verifications.</p>
        """);

    public static string LowBalance(string institutionName, decimal balance, decimal threshold) => Wrap("Low Wallet Balance — TruvoID", $"""
        <div style="background:#fef2f2;border:1px solid #fecaca;border-radius:6px;padding:16px 20px;margin:0 0 24px;">
          <p style="margin:0;color:#991b1b;font-size:14px;font-weight:600;">&#9888; Low Balance Alert</p>
        </div>
        <h1 style="margin:0 0 8px;font-size:22px;color:#1f3864;font-weight:700;">Your wallet balance is running low</h1>
        <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;">The wallet for <strong style="color:#1f3864;">{institutionName}</strong> has dropped below your configured alert threshold.</p>
        <table width="100%" cellpadding="0" cellspacing="0" style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;margin:0 0 24px;">
          <tr>
            <td style="padding:16px 20px;border-bottom:1px solid #e5e7eb;">
              <span style="color:#6b7280;font-size:12px;text-transform:uppercase;font-weight:600;">Current Balance</span><br>
              <span style="color:#dc2626;font-size:24px;font-weight:700;">&#8358;{balance:N2}</span>
            </td>
          </tr>
          <tr>
            <td style="padding:16px 20px;">
              <span style="color:#6b7280;font-size:12px;text-transform:uppercase;font-weight:600;">Alert Threshold</span><br>
              <span style="color:#1f3864;font-size:18px;font-weight:600;">&#8358;{threshold:N2}</span>
            </td>
          </tr>
        </table>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 16px;">
          <tr><td align="center">
            <a href="https://app.truvoid.com/wallet/topup" style="display:inline-block;background:#f5a623;color:#fafafa;text-decoration:none;font-size:14px;font-weight:600;text-transform:uppercase;letter-spacing:1px;padding:14px 32px;border-radius:6px;">Top Up Wallet</a>
          </td></tr>
        </table>
        <p style="margin:0;color:#9ca3af;font-size:12px;text-align:center;">Verification operations will continue until your balance reaches zero. Fund your wallet to avoid interruption.</p>
        """);

    public static string VerificationResult(string institutionName, string verificationType, string status, string callId, decimal cost) => Wrap("Verification Complete — TruvoID", $"""
        <h1 style="margin:0 0 8px;font-size:22px;color:#1f3864;font-weight:700;">Verification Complete</h1>
        <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;">A verification request has been completed for <strong style="color:#1f3864;">{institutionName}</strong>.</p>
        <table width="100%" cellpadding="0" cellspacing="0" style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;margin:0 0 24px;">
          <tr><td style="padding:16px 20px;border-bottom:1px solid #e5e7eb;">
            <span style="color:#6b7280;font-size:12px;text-transform:uppercase;font-weight:600;">Type</span><br>
            <span style="color:#1f3864;font-size:16px;font-weight:600;">{verificationType.ToUpperInvariant()} Verification</span>
          </td></tr>
          <tr><td style="padding:16px 20px;border-bottom:1px solid #e5e7eb;">
            <span style="color:#6b7280;font-size:12px;text-transform:uppercase;font-weight:600;">Result</span><br>
            <span style="color:{(status == "Match" ? "#15803d" : "#dc2626")};font-size:16px;font-weight:600;">{status}</span>
          </td></tr>
          <tr><td style="padding:16px 20px;border-bottom:1px solid #e5e7eb;">
            <span style="color:#6b7280;font-size:12px;text-transform:uppercase;font-weight:600;">Reference ID</span><br>
            <span style="color:#1f3864;font-size:14px;font-family:monospace;">{callId}</span>
          </td></tr>
          <tr><td style="padding:16px 20px;">
            <span style="color:#6b7280;font-size:12px;text-transform:uppercase;font-weight:600;">Cost Deducted</span><br>
            <span style="color:#1f3864;font-size:16px;font-weight:600;">&#8358;{cost:N2}</span>
          </td></tr>
        </table>
        <p style="margin:0;color:#9ca3af;font-size:12px;">View full details in your <a href="https://app.truvoid.com/history" style="color:#1f3864;">verification history</a>.</p>
        """);

    public static string StaffInvitation(string institutionName, string inviterName, string role, string inviteUrl) => Wrap($"You've been invited to join {institutionName} on TruvoID", $"""
        <h1 style="margin:0 0 8px;font-size:22px;color:#1f3864;font-weight:700;">You're invited</h1>
        <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;"><strong style="color:#1f3864;">{inviterName}</strong> has invited you to join <strong style="color:#1f3864;">{institutionName}</strong> on TruvoID as a <strong>{role}</strong>.</p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
          <tr><td align="center">
            <a href="{inviteUrl}" style="display:inline-block;background:#1f3864;color:#fafafa;text-decoration:none;font-size:14px;font-weight:600;text-transform:uppercase;letter-spacing:1px;padding:14px 32px;border-radius:6px;">Accept Invitation</a>
          </td></tr>
        </table>
        <p style="margin:0;color:#9ca3af;font-size:12px;text-align:center;">This invitation expires in 7 days. If you did not expect this invitation, you can safely ignore this email.</p>
        """);

    public static string PasswordReset(string adminName, string resetUrl) => Wrap("Password Reset — TruvoID", $"""
        <h1 style="margin:0 0 8px;font-size:22px;color:#1f3864;font-weight:700;">Reset Your Password</h1>
        <p style="margin:0 0 24px;color:#6b7280;font-size:15px;line-height:1.6;">Hi {adminName}, we received a request to reset your TruvoID password. Click the button below to set a new password.</p>
        <table width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
          <tr><td align="center">
            <a href="{resetUrl}" style="display:inline-block;background:#1f3864;color:#fafafa;text-decoration:none;font-size:14px;font-weight:600;text-transform:uppercase;letter-spacing:1px;padding:14px 32px;border-radius:6px;">Reset Password</a>
          </td></tr>
        </table>
        <div style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;padding:16px 20px;margin:0 0 16px;">
          <p style="margin:0;color:#6b7280;font-size:13px;">This link expires in <strong>1 hour</strong>. If you didn't request a password reset, no action is needed &mdash; your password remains unchanged.</p>
        </div>
        """);
}
