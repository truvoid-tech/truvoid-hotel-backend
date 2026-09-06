namespace TruvoID.Infrastructure.Services;

public static class EmailTemplates
{
    // ── Public URLs ───────────────────────────────────────────────────────────
    private const string AppBaseUrl = "https://gettruvoid.com";
    private const string LogoUrl = "https://gettruvoid.com/assets/images/TruvoID-logo.png";

    // ── Brand tokens ──────────────────────────────────────────────────────────
    private const string Navy = "#1f3864";
    private const string NavyDark = "#132240";
    private const string Gold = "#B8860B";
    private const string GoldLight = "#D4A017";
    private const string SlateGray = "#475467";
    private const string TextMuted = "#667085";
    private const string BorderLight = "#EAECF0";
    private const string SurfaceLight = "#F9FAFB";
    private const string PaperWhite = "#FFFFFF";
    private const string SuccessGreen = "#039855";
    private const string SuccessBg = "#D1FADF";
    private const string ErrorRed = "#B42318";
    private const string ErrorBg = "#FEF3F2";
    private const string WarningAmber = "#B54708";
    private const string WarningBg = "#FFFAEB";

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static string CtaButton(string href, string label, string bgColor = Navy) =>
        $@"<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 24px;"">
          <tr><td align=""center"">
            <a href=""{href}"" style=""display:inline-block;background:{bgColor};color:{PaperWhite};text-decoration:none;font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:1.5px;padding:16px 36px;border-radius:8px;font-family:'Inter','Segoe UI',sans-serif;"" class=""email-cta"">{label}</a>
          </td></tr>
        </table>";

    private static string InfoCard(string bgColor, string borderColor, string textColor, string icon, string text) =>
        $@"<div style=""background:{bgColor};border:1px solid {borderColor};border-radius:8px;padding:16px 20px;margin:0 0 24px;"">
          <p style=""margin:0;color:{textColor};font-size:14px;line-height:1.6;"">{icon} {text}</p>
        </div>";

    private static string KeyValueRow(string label, string value, string valueColor = Navy, bool isLast = false)
    {
        var borderBottom = isLast ? "" : $"border-bottom:1px solid {BorderLight};";
        return $@"<tr><td style=""padding:16px 20px;{borderBottom}"">
          <span style=""color:{TextMuted};font-size:11px;text-transform:uppercase;font-weight:700;letter-spacing:1px;"">{label}</span><br>
          <span style=""color:{valueColor};font-size:16px;font-weight:600;font-family:'Inter','Segoe UI',sans-serif;"">{value}</span>
        </td></tr>";
    }

    private static string Wrap(string title, string body) =>
        $@"<!DOCTYPE html>
<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <meta name=""color-scheme"" content=""light dark"">
  <meta name=""supported-color-schemes"" content=""light dark"">
  <title>{title}</title>
  <style>
    :root {{ color-scheme: light dark; }}

    @media (prefers-color-scheme: dark) {{
      .email-bg {{ background-color: #0f172a !important; }}
      .email-card {{ background-color: #1e293b !important; border-color: #334155 !important; }}
      .email-body-text {{ color: #cbd5e1 !important; }}
      .email-heading {{ color: #f1f5f9 !important; }}
      .email-muted {{ color: #94a3b8 !important; }}
      .email-surface {{ background-color: #1e293b !important; border-color: #334155 !important; }}
    }}

    @media only screen and (max-width: 480px) {{
      .email-bg {{ padding: 16px 8px !important; }}
      .email-card {{ width: 100% !important; }}
      .email-header {{ padding: 20px 20px 0 !important; }}
      .email-header-tag {{ display: none !important; }}
      .email-body {{ padding: 32px 20px !important; }}
      .email-footer {{ padding: 24px 20px !important; }}
      .email-heading {{ font-size: 22px !important; }}
      .email-cta {{ display: block !important; width: 100% !important; box-sizing: border-box !important; text-align: center !important; padding: 16px 24px !important; }}
      .email-card-inner {{ padding: 16px !important; }}
    }}
  </style>
</head>
<body style=""margin:0;padding:0;background:{SurfaceLight};font-family:'Inter','Segoe UI','Helvetica Neue',Arial,sans-serif;-webkit-font-smoothing:antialiased;"" class=""email-bg"">
  <div style=""display:none;max-height:0;overflow:hidden;font-size:1px;line-height:1px;color:{SurfaceLight};"" aria-hidden=""true"">{title}</div>

  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{SurfaceLight};padding:48px 16px;"" class=""email-bg"">
    <tr><td align=""center"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;"" class=""email-card"">

        <tr><td style=""background:linear-gradient(135deg,{Navy} 0%,{NavyDark} 100%);padding:0;"" class=""email-header"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr>
              <td align=""center"" style=""padding:24px 40px 0;"" class=""email-header"">
                <table cellpadding=""0"" cellspacing=""0"" style=""background:{PaperWhite};border-radius:18px;"">
                  <tr>
                    <td style=""padding:12px;"">
                      <img src=""{LogoUrl}"" alt=""TruvoID"" width=""112"" height=""112"" style=""display:block;width:112px;height:112px;"" />
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
            <tr>
              <td align=""center"" style=""padding:14px 40px 26px;"" class=""email-header-tag"">
                <span style=""color:{GoldLight};font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:3px;font-family:'Inter','Segoe UI',sans-serif;"">Identity Verification</span>
              </td>
            </tr>
          </table>
        </td></tr>

        <tr><td style=""height:4px;background:linear-gradient(90deg,{Gold} 0%,{GoldLight} 50%,{Gold} 100%);""></td></tr>

        <tr><td style=""padding:48px 40px;background:{PaperWhite};border-left:1px solid {BorderLight};border-right:1px solid {BorderLight};"" class=""email-body-text email-body"">
          {body}
        </td></tr>

        <tr><td style=""background:{SurfaceLight};border-top:1px solid {BorderLight};border-left:1px solid {BorderLight};border-right:1px solid {BorderLight};border-bottom:1px solid {BorderLight};border-radius:0 0 12px 12px;padding:32px 40px;text-align:center;"" class=""email-footer"">
          <p style=""margin:0 0 8px;color:{TextMuted};font-size:13px;font-weight:600;letter-spacing:0.5px;"">Truvo<span style=""color:{Gold};"">ID</span></p>
          <p style=""margin:0 0 4px;color:{TextMuted};font-size:11px;line-height:1.6;"">Institutional Identity Verification Platform</p>
          <p style=""margin:0;color:{TextMuted};font-size:11px;line-height:1.6;"">Automated notification &middot; Please do not reply to this email</p>
        </td></tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";

    // ══════════════════════════════════════════════════════════════════════════
    // WELCOME
    // ══════════════════════════════════════════════════════════════════════════

    public static string Welcome(string institutionName, string adminName) =>
        Wrap("Welcome to TruvoID",
            $@"<h1 style=""margin:0 0 8px;font-size:26px;font-weight:800;color:{Navy};letter-spacing:-0.5px;"" class=""email-heading"">Welcome, {adminName} 👋</h1>
        <p style=""margin:0 0 32px;color:{SlateGray};font-size:15px;line-height:1.7;"" class=""email-body-text"">
          Your institution <strong style=""color:{Navy};"">{institutionName}</strong> has been successfully registered on TruvoID. Our compliance team is now reviewing your application.
        </p>

        <div style=""background:{WarningBg};border:1px solid #FEC84B;border-radius:10px;padding:20px 24px;margin:0 0 32px;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr>
              <td width=""36"" valign=""top"" style=""padding-right:12px;""><span style=""font-size:20px;"">⏳</span></td>
              <td>
                <p style=""margin:0 0 4px;color:{WarningAmber};font-size:14px;font-weight:700;"">Application Under Review</p>
                <p style=""margin:0;color:{SlateGray};font-size:13px;line-height:1.6;"">
                  Document review typically takes <strong>1–2 business days</strong>. You'll receive an email when your account is approved and ready for live verifications.
                </p>
              </td>
            </tr>
          </table>
        </div>

        <p style=""margin:0 0 16px;color:{Navy};font-size:14px;font-weight:700;letter-spacing:0.5px;"" class=""email-heading"">WHAT YOU'LL GET ACCESS TO</p>
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 32px;"">
          <tr><td width=""36"" valign=""top"" style=""padding:8px 12px 8px 0;""><span style=""color:{SuccessGreen};font-size:16px;"">✓</span></td>
              <td style=""padding:8px 0;color:{SlateGray};font-size:14px;line-height:1.6;"" class=""email-body-text"">NIN, BVN &amp; Phone verification APIs</td></tr>
          <tr><td width=""36"" valign=""top"" style=""padding:8px 12px 8px 0;""><span style=""color:{SuccessGreen};font-size:16px;"">✓</span></td>
              <td style=""padding:8px 0;color:{SlateGray};font-size:14px;line-height:1.6;"" class=""email-body-text"">Wallet funding &amp; real-time usage dashboard</td></tr>
          <tr><td width=""36"" valign=""top"" style=""padding:8px 12px 8px 0;""><span style=""color:{SuccessGreen};font-size:16px;"">✓</span></td>
              <td style=""padding:8px 0;color:{SlateGray};font-size:14px;line-height:1.6;"" class=""email-body-text"">Staff management &amp; team access controls</td></tr>
        </table>

        <p style=""margin:0;color:{TextMuted};font-size:13px;line-height:1.7;"" class=""email-muted"">
          Questions? Reach us at <a href=""mailto:support@truvoid.com"" style=""color:{Navy};font-weight:600;text-decoration:none;"">support@truvoid.com</a>
        </p>");

    // ══════════════════════════════════════════════════════════════════════════
    // APPROVED
    // ══════════════════════════════════════════════════════════════════════════

    public static string Approved(string institutionName, string adminName) =>
        Wrap("Your Account Is Approved — TruvoID",
            $@"<div style=""text-align:center;margin-bottom:32px;"">
          <div style=""display:inline-block;background:{SuccessBg};border:2px solid {SuccessGreen};border-radius:50%;width:72px;height:72px;line-height:72px;font-size:32px;"">✓</div>
        </div>

        <h1 style=""margin:0 0 12px;font-size:28px;font-weight:800;color:{Navy};text-align:center;letter-spacing:-0.5px;"" class=""email-heading"">You're Approved!</h1>
        <p style=""margin:0 0 32px;color:{SlateGray};font-size:15px;line-height:1.7;text-align:center;"" class=""email-body-text"">
          <strong style=""color:{Navy};"">{institutionName}</strong> has been verified and activated. You now have full access to TruvoID's identity verification infrastructure.
        </p>

        {CtaButton($"{AppBaseUrl}/dashboard", "Go to Dashboard →", Navy)}

        <div style=""background:{SurfaceLight};border:1px solid {BorderLight};border-radius:10px;padding:24px;margin:0 0 24px;"">
          <p style=""margin:0 0 12px;color:{Navy};font-size:13px;font-weight:700;letter-spacing:1px;text-transform:uppercase;"">Quick Start</p>
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr><td width=""28"" valign=""top"" style=""padding:6px 10px 6px 0;color:{Gold};font-size:14px;font-weight:800;"">1</td>
                <td style=""padding:6px 0;color:{SlateGray};font-size:14px;line-height:1.6;"" class=""email-body-text"">Log in with your admin credentials</td></tr>
            <tr><td width=""28"" valign=""top"" style=""padding:6px 10px 6px 0;color:{Gold};font-size:14px;font-weight:800;"">2</td>
                <td style=""padding:6px 0;color:{SlateGray};font-size:14px;line-height:1.6;"" class=""email-body-text"">Fund your wallet to start verifications</td></tr>
            <tr><td width=""28"" valign=""top"" style=""padding:6px 10px 6px 0;color:{Gold};font-size:14px;font-weight:800;"">3</td>
                <td style=""padding:6px 0;color:{SlateGray};font-size:14px;line-height:1.6;"" class=""email-body-text"">Generate an API key &amp; integrate in minutes</td></tr>
          </table>
        </div>

        <p style=""margin:0;color:{TextMuted};font-size:13px;line-height:1.7;"" class=""email-muted"">
          Need help getting started? Contact us at <a href=""mailto:support@truvoid.com"" style=""color:{Navy};font-weight:600;text-decoration:none;"">support@truvoid.com</a>
        </p>");

    // ══════════════════════════════════════════════════════════════════════════
    // PASSWORD RESET
    // ══════════════════════════════════════════════════════════════════════════

    public static string PasswordReset(string adminName, string resetUrl) =>
        Wrap("Reset Your Password — TruvoID",
            $@"<div style=""text-align:center;margin-bottom:24px;"">
          <div style=""display:inline-block;background:{SurfaceLight};border:1px solid {BorderLight};border-radius:50%;width:64px;height:64px;line-height:64px;font-size:28px;"">🔐</div>
        </div>

        <h1 style=""margin:0 0 8px;font-size:24px;font-weight:800;color:{Navy};text-align:center;letter-spacing:-0.5px;"" class=""email-heading"">Reset Your Password</h1>
        <p style=""margin:0 0 32px;color:{SlateGray};font-size:15px;line-height:1.7;text-align:center;"" class=""email-body-text"">
          Hi <strong style=""color:{Navy};"">{adminName}</strong>, we received a request to reset your TruvoID password. Click the button below to set a new one.
        </p>

        {CtaButton(resetUrl, "Reset Password →", Navy)}

        <div style=""background:{SurfaceLight};border:1px solid {BorderLight};border-radius:10px;padding:20px 24px;margin:0 0 24px;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr>
              <td width=""36"" valign=""top"" style=""padding-right:12px;""><span style=""font-size:18px;"">⏱️</span></td>
              <td>
                <p style=""margin:0;color:{SlateGray};font-size:13px;line-height:1.7;"" class=""email-body-text"">
                  This link expires in <strong style=""color:{Navy};"">1 hour</strong>. If you didn't request a password reset, no action is needed — your password remains unchanged.
                </p>
              </td>
            </tr>
          </table>
        </div>

        <p style=""margin:0;color:{TextMuted};font-size:13px;line-height:1.7;"" class=""email-muted"">
          If you didn't request this, please contact <a href=""mailto:support@truvoid.com"" style=""color:{Navy};font-weight:600;text-decoration:none;"">support@truvoid.com</a>
        </p>");

    // ══════════════════════════════════════════════════════════════════════════
    // STAFF INVITATION
    // ══════════════════════════════════════════════════════════════════════════

    public static string StaffInvitation(string institutionName, string inviterName, string role, string inviteUrl) =>
        Wrap($@"You're Invited to Join {institutionName} on TruvoID",
            $@"<div style=""text-align:center;margin-bottom:24px;"">
          <div style=""display:inline-block;background:{SurfaceLight};border:1px solid {BorderLight};border-radius:50%;width:64px;height:64px;line-height:64px;font-size:28px;"">👥</div>
        </div>

        <h1 style=""margin:0 0 8px;font-size:24px;font-weight:800;color:{Navy};text-align:center;letter-spacing:-0.5px;"" class=""email-heading"">You're Invited</h1>
        <p style=""margin:0 0 32px;color:{SlateGray};font-size:15px;line-height:1.7;text-align:center;"" class=""email-body-text"">
          <strong style=""color:{Navy};"">{inviterName}</strong> has invited you to join
          <strong style=""color:{Navy};"">{institutionName}</strong> on TruvoID.
        </p>

        <div style=""text-align:center;margin:0 0 32px;"">
          <span style=""display:inline-block;background:{Navy};color:{PaperWhite};font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:2px;padding:8px 20px;border-radius:20px;"">{role}</span>
        </div>

        {CtaButton(inviteUrl, "Accept Invitation →", Gold)}

        <div style=""background:{SurfaceLight};border:1px solid {BorderLight};border-radius:10px;padding:20px 24px;margin:0 0 24px;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr>
              <td width=""36"" valign=""top"" style=""padding-right:12px;""><span style=""font-size:18px;"">ℹ️</span></td>
              <td>
                <p style=""margin:0;color:{SlateGray};font-size:13px;line-height:1.7;"" class=""email-body-text"">
                  This invitation expires in <strong style=""color:{Navy};"">7 days</strong>. If you did not expect this invitation, you can safely ignore this email.
                </p>
              </td>
            </tr>
          </table>
        </div>");

    // ══════════════════════════════════════════════════════════════════════════
    // LOW BALANCE
    // ══════════════════════════════════════════════════════════════════════════

    public static string LowBalance(string institutionName, decimal balance, decimal threshold) =>
        Wrap("Low Wallet Balance — TruvoID",
            $@"<div style=""text-align:center;margin-bottom:24px;"">
          <div style=""display:inline-block;background:{ErrorBg};border:2px solid #FECDD3;border-radius:50%;width:64px;height:64px;line-height:64px;font-size:28px;"">⚠️</div>
        </div>

        <h1 style=""margin:0 0 12px;font-size:24px;font-weight:800;color:{Navy};text-align:center;letter-spacing:-0.5px;"" class=""email-heading"">Low Balance Alert</h1>
        <p style=""margin:0 0 32px;color:{SlateGray};font-size:15px;line-height:1.7;text-align:center;"" class=""email-body-text"">
          The wallet for <strong style=""color:{Navy};"">{institutionName}</strong> has dropped below your configured alert threshold.
        </p>

        <div style=""background:{SurfaceLight};border:1px solid {BorderLight};border-radius:10px;margin:0 0 32px;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            <tr><td style=""padding:20px 24px;border-bottom:1px solid {BorderLight};"">
              <span style=""color:{TextMuted};font-size:11px;text-transform:uppercase;font-weight:700;letter-spacing:1px;"">Current Balance</span><br>
              <span style=""color:{ErrorRed};font-size:28px;font-weight:800;font-family:'Inter','Segoe UI',sans-serif;"">₦{balance:N2}</span>
            </td></tr>
            <tr><td style=""padding:20px 24px;"">
              <span style=""color:{TextMuted};font-size:11px;text-transform:uppercase;font-weight:700;letter-spacing:1px;"">Alert Threshold</span><br>
              <span style=""color:{Navy};font-size:20px;font-weight:700;font-family:'Inter','Segoe UI',sans-serif;"">₦{threshold:N2}</span>
            </td></tr>
          </table>
        </div>

        {CtaButton($"{AppBaseUrl}/wallet/topup", "Top Up Wallet →", Gold)}

        <div style=""background:{WarningBg};border:1px solid #FEC84B;border-radius:10px;padding:16px 20px;margin:0 0 24px;"">
          <p style=""margin:0;color:{WarningAmber};font-size:13px;line-height:1.6;font-weight:500;"">
            Verification operations will continue until your balance reaches zero. Fund your wallet to avoid service interruption.
          </p>
        </div>");

    // ══════════════════════════════════════════════════════════════════════════
    // VERIFICATION RESULT
    // ══════════════════════════════════════════════════════════════════════════

    public static string VerificationResult(string institutionName, string verificationType, string status, string callId, decimal cost)
    {
        var isMatch = string.Equals(status, "Match", StringComparison.OrdinalIgnoreCase);
        var statusColor = isMatch ? SuccessGreen : ErrorRed;
        var statusBg = isMatch ? SuccessBg : ErrorBg;
        var statusIcon = isMatch ? "✓" : "✗";
        var statusLabel = isMatch ? "Match" : "No Match";
        var statusCard = isMatch
            ? InfoCard(SuccessBg, "#A3E635", SuccessGreen, "✅", "The submitted data matched the government database successfully.")
            : InfoCard(ErrorBg, "#FECDD3", ErrorRed, "❌", "The submitted data did not match the government database. Review and try again.");

        return Wrap(
            $"Verification Complete — {verificationType.ToUpperInvariant()} — TruvoID",
            $@"<div style=""text-align:center;margin-bottom:24px;"">
          <div style=""display:inline-block;background:{statusBg};border:2px solid {statusColor};border-radius:50%;width:72px;height:72px;line-height:72px;font-size:32px;color:{statusColor};font-weight:700;"">{statusIcon}</div>
        </div>

        <h1 style=""margin:0 0 8px;font-size:26px;font-weight:800;color:{Navy};text-align:center;letter-spacing:-0.5px;"" class=""email-heading"">Verification Complete</h1>
        <p style=""margin:0 0 32px;color:{SlateGray};font-size:15px;line-height:1.7;text-align:center;"" class=""email-body-text"">
          A <strong style=""color:{Navy};"">{verificationType.ToUpperInvariant()}</strong> verification request has been completed for
          <strong style=""color:{Navy};"">{institutionName}</strong>.
        </p>

        <div style=""background:{SurfaceLight};border:1px solid {BorderLight};border-radius:10px;margin:0 0 32px;"">
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
            {KeyValueRow("Verification Type", $"{verificationType.ToUpperInvariant()} Verification")}
            {KeyValueRow("Result", $"<span style=\\\"color:{statusColor};font-weight:700;\\\">{statusLabel}</span>")}
            {KeyValueRow("Reference ID", $"<span style=\\\"font-family:monospace;font-size:13px;\\\">{callId}</span>")}
            {KeyValueRow("Cost Deducted", $"₦{cost:N2}", Navy, isLast: true)}
          </table>
        </div>

        {statusCard}

        <p style=""margin:0;color:{TextMuted};font-size:13px;line-height:1.7;"" class=""email-muted"">
          View full details in your <a href=""{AppBaseUrl}/history"" style=""color:{Navy};font-weight:600;text-decoration:none;"">verification history</a>.
        </p>");
    }
}
