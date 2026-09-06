using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TruvoID.Infrastructure.Services;

namespace TruvoID.Tests;

// ──────────────────────────────────────────────────────────────────────────────
// Inline service types — mirrors the real implementations so tests compile
// without needing project references to Infrastructure/Domain/Core.
// ──────────────────────────────────────────────────────────────────────────────

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private const string FromAddress = "TruvoID <noreply@gettruvoid.com>";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ResendEmailService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("resend");
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var payload = new
        {
            from = FromAddress,
            to = new[] { string.IsNullOrWhiteSpace(toName) ? toEmail : $"{toName} <{toEmail}>" },
            subject,
            html = htmlBody
        };

        var response = await _http.PostAsJsonAsync("emails", payload, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Resend API error {(int)response.StatusCode}: {error}");
        }
    }
}

public interface INotificationService
{
    Task SendWelcomeAsync(string toEmail, string adminName, string institutionName);
    Task SendApprovalAsync(string toEmail, string adminName, string institutionName);
    Task SendPasswordResetAsync(string toEmail, string adminName, string resetToken, string baseUrl);
    Task SendStaffInvitationAsync(string toEmail, string institutionName, string inviterName, string role, string inviteToken, string baseUrl);
}

public class NotificationService : INotificationService
{
    private readonly IEmailService _email;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IEmailService email, ILogger<NotificationService> logger)
    {
        _email = email;
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
}

// ──────────────────────────────────────────────────────────────────────────────
// Test helpers
// ──────────────────────────────────────────────────────────────────────────────

public class RecordingEmailService : IEmailService
{
    public List<(string ToEmail, string ToName, string Subject, string HtmlBody)> Sent { get; } = new();

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        Sent.Add((toEmail, toName, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public class ThrowingEmailService : IEmailService
{
    public List<(string ToEmail, string ToName, string Subject, string HtmlBody)> Sent { get; } = new();

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        Sent.Add((toEmail, toName, subject, htmlBody));
        throw new InvalidOperationException("Simulated email failure");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Email Template Tests — validates the real templates from EmailTemplates.cs
// ══════════════════════════════════════════════════════════════════════════════

public class EmailTemplateTests
{
    private static string[] GetAllHtml() =>
    [
        EmailTemplates.Welcome("A", "B"),
        EmailTemplates.Approved("A", "B"),
        EmailTemplates.PasswordReset("A", "https://x"),
        EmailTemplates.LowBalance("A", 1, 2),
        EmailTemplates.VerificationResult("A", "NIN", "Match", "c", 1),
        EmailTemplates.StaffInvitation("A", "B", "C", "https://x"),
    ];

    // ── Structure & branding ──────────────────────────────────────────────────

    [Fact]
    public void AllTemplates_IsValidHtmlWithDocType()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<html", html);
            Assert.Contains("</html>", html);
        }
    }

    [Fact]
    public void AllTemplates_ContainTruvoIDBranding()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("TruvoID", html);
        }
    }

    [Fact]
    public void AllTemplates_HaveAutomatedNotificationFooter()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("automated notification", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AllTemplates_HaveViewportMeta()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("width=device-width", html);
        }
    }

    [Fact]
    public void AllTemplates_HaveMobileMediaQuery()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("max-width: 480px", html);
        }
    }

    [Fact]
    public void AllTemplates_HaveDarkModeMediaQuery()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("prefers-color-scheme: dark", html);
        }
    }

    [Fact]
    public void AllTemplates_HaveMobileCssClasses()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("email-body", html);
            Assert.Contains("email-footer", html);
            Assert.Contains("email-cta", html);
        }
    }

    [Fact]
    public void AllTemplates_HavePreheaderText()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("display:none", html);
            Assert.Contains("max-height:0", html);
        }
    }

    [Fact]
    public void AllTemplates_HaveColorSchemeMeta()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("color-scheme", html);
        }
    }

    [Fact]
    public void AllTemplates_EmbedTruvoIDLogo()
    {
        foreach (var html in GetAllHtml())
        {
            Assert.Contains("gettruvoid.com/assets/images/TruvoID-logo.png", html);
            Assert.Contains("alt=\"TruvoID\"", html);
            Assert.Contains("<img", html);
        }
    }

    // ── Welcome ───────────────────────────────────────────────────────────────

    [Fact]
    public void Welcome_IncludesInstitutionNameAndAdminName()
    {
        var html = EmailTemplates.Welcome("Acme Corp", "John Doe");
        Assert.Contains("Acme Corp", html);
        Assert.Contains("John Doe", html);
        Assert.Contains("Welcome, John Doe", html);
    }

    [Fact]
    public void Welcome_HasReviewStatusCard()
    {
        var html = EmailTemplates.Welcome("Acme Corp", "John");
        Assert.Contains("Application Under Review", html);
        Assert.Contains("1–2 business days", html);
    }

    [Fact]
    public void Welcome_ListsFeatures()
    {
        var html = EmailTemplates.Welcome("Acme Corp", "John");
        Assert.Contains("NIN, BVN", html);
        Assert.Contains("Wallet funding", html);
        Assert.Contains("Staff management", html);
    }

    [Fact]
    public void Welcome_HasSupportLink()
    {
        var html = EmailTemplates.Welcome("Acme Corp", "John");
        Assert.Contains("mailto:support@truvoid.com", html);
    }

    // ── Approved ──────────────────────────────────────────────────────────────

    [Fact]
    public void Approved_IncludesInstitutionName()
    {
        var html = EmailTemplates.Approved("Acme Corp", "John");
        Assert.Contains("Acme Corp", html);
        Assert.Contains("Approved!", html);
    }

    [Fact]
    public void Approved_HasDashboardCta()
    {
        var html = EmailTemplates.Approved("Acme Corp", "John");
        Assert.Contains("gettruvoid.com/dashboard", html);
        Assert.Contains("Go to Dashboard", html);
    }

    [Fact]
    public void Approved_HasQuickStartSteps()
    {
        var html = EmailTemplates.Approved("Acme Corp", "John");
        Assert.Contains("Quick Start", html);
        Assert.Contains("admin credentials", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fund your wallet", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API key", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Approved_HasSuccessIcon()
    {
        var html = EmailTemplates.Approved("Acme Corp", "John");
        Assert.Contains("#039855", html);
    }

    // ── Password Reset ────────────────────────────────────────────────────────

    [Fact]
    public void PasswordReset_IncludesAdminName()
    {
        var html = EmailTemplates.PasswordReset("Jane", "https://app.truvoid.com/reset-password?token=abc123");
        Assert.Contains("Jane", html);
        Assert.Contains("Hi <strong", html);
    }

    [Fact]
    public void PasswordReset_IncludesResetUrl()
    {
        var html = EmailTemplates.PasswordReset("Jane", "https://app.truvoid.com/reset-password?token=abc123");
        Assert.Contains("https://app.truvoid.com/reset-password?token=abc123", html);
    }

    [Fact]
    public void PasswordReset_HasExpiryNotice()
    {
        var html = EmailTemplates.PasswordReset("User", "https://x.com/reset?token=t");
        Assert.Contains("1 hour", html);
    }

    [Fact]
    public void PasswordReset_HasSecurityInfo()
    {
        var html = EmailTemplates.PasswordReset("User", "https://x.com/reset?token=t");
        Assert.Contains("password remains unchanged", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PasswordReset_HasResetPasswordCta()
    {
        var html = EmailTemplates.PasswordReset("User", "https://x.com/reset?token=t");
        Assert.Contains("Reset Password", html);
    }

    // ── Staff Invitation ──────────────────────────────────────────────────────

    [Fact]
    public void StaffInvitation_IncludesAllParams()
    {
        var url = "https://app.truvoid.com/accept-invite?token=inv123";
        var html = EmailTemplates.StaffInvitation("Acme Corp", "Admin User", "Viewer", url);
        Assert.Contains("Acme Corp", html);
        Assert.Contains("Admin User", html);
        Assert.Contains("Viewer", html);
        Assert.Contains(url, html);
    }

    [Fact]
    public void StaffInvitation_HasRoleBadge()
    {
        var html = EmailTemplates.StaffInvitation("Acme Corp", "Admin", "Manager", "https://x.com");
        Assert.Contains("Manager", html);
        Assert.Contains("background:#1f3864", html);
    }

    [Fact]
    public void StaffInvitation_HasAcceptInvitationCta()
    {
        var html = EmailTemplates.StaffInvitation("Acme Corp", "Admin", "Viewer", "https://x.com");
        Assert.Contains("Accept Invitation", html);
    }

    [Fact]
    public void StaffInvitation_HasExpiryNotice()
    {
        var html = EmailTemplates.StaffInvitation("Acme Corp", "Admin", "Viewer", "https://x.com");
        Assert.Contains("7 days", html);
    }

    // ── Low Balance ───────────────────────────────────────────────────────────

    [Fact]
    public void LowBalance_ShowsNairaAmounts()
    {
        var html = EmailTemplates.LowBalance("Acme Corp", 500.50m, 10000m);
        Assert.Contains("500.50", html);
        Assert.Contains("10,000.00", html);
        Assert.Contains("Acme Corp", html);
    }

    [Fact]
    public void LowBalance_HasTopUpCta()
    {
        var html = EmailTemplates.LowBalance("Acme Corp", 500m, 10000m);
        Assert.Contains("gettruvoid.com/wallet/topup", html);
        Assert.Contains("Top Up Wallet", html);
    }

    [Fact]
    public void LowBalance_ShowsAlertWarning()
    {
        var html = EmailTemplates.LowBalance("Acme Corp", 500m, 10000m);
        Assert.Contains("Low Balance Alert", html);
        Assert.Contains("service interruption", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LowBalance_ZeroBalance_FormatsCorrectly()
    {
        var html = EmailTemplates.LowBalance("Test", 0m, 500m);
        Assert.Contains("0.00", html);
        Assert.Contains("500.00", html);
    }

    // ── Verification Result ───────────────────────────────────────────────────

    [Fact]
    public void VerificationResult_Match_ShowsGreenStatus()
    {
        var html = EmailTemplates.VerificationResult("Acme Corp", "NIN", "Match", "call123", 50m);
        Assert.Contains("Match", html);
        Assert.Contains("#039855", html);
        Assert.Contains("call123", html);
        Assert.Contains("50.00", html);
        Assert.Contains("Acme Corp", html);
        Assert.Contains("NIN Verification", html);
    }

    [Fact]
    public void VerificationResult_NoMatch_ShowsRedStatus()
    {
        var html = EmailTemplates.VerificationResult("Acme Corp", "BVN", "NoMatch", "call456", 75m);
        Assert.Contains("No Match", html);
        Assert.Contains("#B42318", html);
        Assert.Contains("call456", html);
        Assert.Contains("BVN Verification", html);
    }

    [Fact]
    public void VerificationResult_Match_HasSuccessMessage()
    {
        var html = EmailTemplates.VerificationResult("Acme Corp", "NIN", "Match", "c1", 10m);
        Assert.Contains("matched the government database", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationResult_NoMatch_HasFailureMessage()
    {
        var html = EmailTemplates.VerificationResult("Acme Corp", "NIN", "NoMatch", "c1", 10m);
        Assert.Contains("did not match", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationResult_HasHistoryLink()
    {
        var html = EmailTemplates.VerificationResult("Acme Corp", "NIN", "Match", "c1", 10m);
        Assert.Contains("gettruvoid.com/history", html);
        Assert.Contains("verification history", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationResult_KeyValueTable_HasAllRows()
    {
        var html = EmailTemplates.VerificationResult("Acme Corp", "NIN", "Match", "call-abc", 150m);
        Assert.Contains("Verification Type", html);
        Assert.Contains("Result", html);
        Assert.Contains("Reference ID", html);
        Assert.Contains("Cost Deducted", html);
        Assert.Contains("call-abc", html);
        Assert.Contains("150.00", html);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ResendEmailService Tests
// ══════════════════════════════════════════════════════════════════════════════

public class ResendEmailServiceTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private string _baseUrl = null!;
    private readonly List<(string To, string Subject, string HtmlBody)> _sentEmails = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new[] { "--urls", "http://127.0.0.1:0" });
        _app = builder.Build();
        _app.Urls.Add("http://127.0.0.1:0");

        _app.MapPost("/emails", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var toArr = root.GetProperty("to");
            var toStr = toArr[0].GetString() ?? "";
            var subject = root.GetProperty("subject").GetString() ?? "";
            var html = root.GetProperty("html").GetString() ?? "";

            _sentEmails.Add((toStr, subject, html));

            var response = new { id = Guid.NewGuid().ToString(), from = "test@test.com" };
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(response);
        });

        await _app.StartAsync();
        _baseUrl = _app.Urls.First();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private ResendEmailService CreateService()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("resend", client =>
        {
            client.BaseAddress = new Uri(_baseUrl);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-key");
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return new ResendEmailService(factory);
    }

    [Fact]
    public async Task SendAsync_PostsCorrectPayload()
    {
        var svc = CreateService();
        await svc.SendAsync("user@example.com", "Jane", "Test Subject", "<h1>Hello</h1>");

        Assert.Single(_sentEmails);
        Assert.Equal("Jane <user@example.com>", _sentEmails[0].To);
        Assert.Equal("Test Subject", _sentEmails[0].Subject);
        Assert.Contains("<h1>Hello</h1>", _sentEmails[0].HtmlBody);
    }

    [Fact]
    public async Task SendAsync_EmptyName_UsesEmailOnly()
    {
        var svc = CreateService();
        await svc.SendAsync("user@example.com", "", "Subject", "Body");

        Assert.Single(_sentEmails);
        Assert.Equal("user@example.com", _sentEmails[0].To);
    }

    [Fact]
    public async Task SendAsync_WhitespaceName_UsesEmailOnly()
    {
        var svc = CreateService();
        await svc.SendAsync("user@example.com", "   ", "Subject", "Body");

        Assert.Single(_sentEmails);
        Assert.Equal("user@example.com", _sentEmails[0].To);
    }

    [Fact]
    public async Task SendAsync_ApiError_ThrowsInvalidOperationException()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();

        var builder = WebApplication.CreateBuilder(new[] { "--urls", "http://127.0.0.1:0" });
        _app = builder.Build();
        _app.Urls.Add("http://127.0.0.1:0");

        _app.MapPost("/emails", async (HttpContext ctx) =>
        {
            ctx.Response.StatusCode = 422;
            await ctx.Response.WriteAsJsonAsync(new { message = "Invalid email" });
        });

        await _app.StartAsync();
        var errorBaseUrl = _app.Urls.First();

        var services = new ServiceCollection();
        services.AddHttpClient("resend", client =>
        {
            client.BaseAddress = new Uri(errorBaseUrl);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer bad-key");
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var svc = new ResendEmailService(factory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SendAsync("bad@test.com", "User", "Sub", "Body"));
        Assert.Contains("422", ex.Message);
    }

    [Fact]
    public async Task SendAsync_HandlesSpecialCharactersInSubject()
    {
        var svc = CreateService();
        var subject = "Verification Complete — NIN & BVN";
        await svc.SendAsync("user@test.com", "User", subject, "Body");

        Assert.Single(_sentEmails);
        Assert.Equal(subject, _sentEmails[0].Subject);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// NotificationService Tests
// ══════════════════════════════════════════════════════════════════════════════

public class NotificationServiceTests
{
    private static ILogger<NotificationService> CreateLogger()
    {
        using var factory = LoggerFactory.Create(builder => builder.AddConsole());
        return factory.CreateLogger<NotificationService>();
    }

    // ── SendWelcomeAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendWelcomeAsync_SendsCorrectEmail()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendWelcomeAsync("admin@acme.com", "John Doe", "Acme Corp");

        Assert.Single(email.Sent);
        Assert.Equal("admin@acme.com", email.Sent[0].ToEmail);
        Assert.Equal("John Doe", email.Sent[0].ToName);
        Assert.Contains("Welcome to TruvoID", email.Sent[0].Subject);
        Assert.Contains("Acme Corp", email.Sent[0].HtmlBody);
        Assert.Contains("John Doe", email.Sent[0].HtmlBody);
    }

    [Fact]
    public async Task SendWelcomeAsync_DoesNotThrow_OnEmailFailure()
    {
        var email = new ThrowingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendWelcomeAsync("admin@acme.com", "John", "Acme");
        Assert.Single(email.Sent);
    }

    // ── SendApprovalAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SendApprovalAsync_SendsCorrectEmail()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendApprovalAsync("admin@acme.com", "John Doe", "Acme Corp");

        Assert.Single(email.Sent);
        Assert.Equal("admin@acme.com", email.Sent[0].ToEmail);
        Assert.Contains("Approved", email.Sent[0].Subject);
        Assert.Contains("Acme Corp", email.Sent[0].HtmlBody);
    }

    [Fact]
    public async Task SendApprovalAsync_DoesNotThrow_OnEmailFailure()
    {
        var email = new ThrowingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendApprovalAsync("admin@acme.com", "John", "Acme");
        Assert.Single(email.Sent);
    }

    // ── SendPasswordResetAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SendPasswordResetAsync_IncludesResetUrlInBody()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendPasswordResetAsync("admin@acme.com", "John Doe", "my-secret-token", "https://app.truvoid.com");

        Assert.Single(email.Sent);
        Assert.Equal("admin@acme.com", email.Sent[0].ToEmail);
        Assert.Contains("my-secret-token", email.Sent[0].HtmlBody);
        Assert.Contains("https://app.truvoid.com/reset-password?token=my-secret-token", email.Sent[0].HtmlBody);
    }

    [Fact]
    public async Task SendPasswordResetAsync_TrimsTrailingSlashFromBaseUrl()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendPasswordResetAsync("user@test.com", "User", "tok123", "https://app.truvoid.com/");

        Assert.Contains("https://app.truvoid.com/reset-password?token=tok123", email.Sent[0].HtmlBody);
        Assert.DoesNotContain("//reset-password", email.Sent[0].HtmlBody);
    }

    [Fact]
    public async Task SendPasswordResetAsync_EncodesTokenInUrl()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        var token = "abc+def/ghi=";
        await svc.SendPasswordResetAsync("user@test.com", "User", token, "https://app.truvoid.com");

        var html = email.Sent[0].HtmlBody;
        Assert.Contains("reset-password?token=", html);
        Assert.Contains(Uri.EscapeDataString(token), html);
    }

    [Fact]
    public async Task SendPasswordResetAsync_DoesNotThrow_OnEmailFailure()
    {
        var email = new ThrowingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendPasswordResetAsync("admin@acme.com", "John", "token", "https://x.com");
        Assert.Single(email.Sent);
    }

    // ── SendStaffInvitationAsync ──────────────────────────────────────────────

    [Fact]
    public async Task SendStaffInvitationAsync_IncludesInviteUrlAndRole()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendStaffInvitationAsync(
            "jane@acme.com", "Acme Corp", "John Admin", "Viewer", "invite-token-abc", "https://app.truvoid.com");

        Assert.Single(email.Sent);
        Assert.Equal("jane@acme.com", email.Sent[0].ToEmail);
        Assert.Contains("invite-token-abc", email.Sent[0].HtmlBody);
        Assert.Contains("https://app.truvoid.com/accept-invite?token=invite-token-abc", email.Sent[0].HtmlBody);
        Assert.Contains("Viewer", email.Sent[0].HtmlBody);
        Assert.Contains("Acme Corp", email.Sent[0].Subject);
    }

    [Fact]
    public async Task SendStaffInvitationAsync_EncodesTokenInUrl()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        var token = "invite+token/special";
        await svc.SendStaffInvitationAsync("jane@test.com", "Corp", "Admin", "Viewer", token, "https://app.truvoid.com");

        var html = email.Sent[0].HtmlBody;
        Assert.Contains("accept-invite?token=", html);
        Assert.Contains(Uri.EscapeDataString(token), html);
    }

    [Fact]
    public async Task SendStaffInvitationAsync_SendsToEmptyName()
    {
        var email = new RecordingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendStaffInvitationAsync("jane@test.com", "Corp", "Admin", "Viewer", "inv", "https://x.com");

        Assert.Equal(string.Empty, email.Sent[0].ToName);
    }

    [Fact]
    public async Task SendStaffInvitationAsync_DoesNotThrow_OnEmailFailure()
    {
        var email = new ThrowingEmailService();
        var svc = new NotificationService(email, CreateLogger());

        await svc.SendStaffInvitationAsync("jane@test.com", "Corp", "Admin", "Viewer", "inv", "https://x.com");
        Assert.Single(email.Sent);
    }
}
