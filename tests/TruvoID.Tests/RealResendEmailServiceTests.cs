using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RealResendEmailService = TruvoID.Infrastructure.Services.ResendEmailService;

namespace TruvoID.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// Tests that drive the REAL ResendEmailService from src/TruvoID.Infrastructure
// (linked into this project via the csproj) against a fake HTTP transport,
// so no network call or real Resend API key is required.
// ══════════════════════════════════════════════════════════════════════════════

public class CapturingHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"msg_123\"}", Encoding.UTF8, "application/json")
        };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Responder(request));
    }
}

public class RealResendEmailServiceTests
{
    private static (RealResendEmailService Service, CapturingHttpMessageHandler Handler) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = new CapturingHttpMessageHandler();
        if (responder is not null)
            handler.Responder = responder;

        var services = new ServiceCollection();
        services.AddHttpClient("resend", client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-key");
        }).ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return (new RealResendEmailService(factory), handler);
    }

    private static async Task<JsonDocument> ReadPayloadAsync(HttpContent content)
    {
        var body = await content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    [Fact]
    public async Task SendAsync_PostsToResendEmailsEndpoint_WithCorrectPayload()
    {
        var (svc, handler) = CreateService();

        await svc.SendAsync("jane@acme.com", "Jane Doe", "Welcome to TruvoID", "<h1>Hi Jane</h1>");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri!.AbsoluteUri);

        // Bearer auth header configured on the named client (as AddNotificationServices does)
        Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());

        using var doc = await ReadPayloadAsync(request.Content!);
        var root = doc.RootElement;
        Assert.Equal("TruvoID <noreply@truvoid.com>", root.GetProperty("from").GetString());
        Assert.Equal("Jane Doe <jane@acme.com>", root.GetProperty("to")[0].GetString());
        Assert.Equal("Welcome to TruvoID", root.GetProperty("subject").GetString());
        Assert.Equal("<h1>Hi Jane</h1>", root.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAsync_EmptyName_UsesEmailOnly()
    {
        var (svc, handler) = CreateService();

        await svc.SendAsync("user@example.com", "", "Subject", "Body");

        var request = Assert.Single(handler.Requests);
        using var doc = await ReadPayloadAsync(request.Content!);
        Assert.Equal("user@example.com", doc.RootElement.GetProperty("to")[0].GetString());
    }

    [Fact]
    public async Task SendAsync_WhitespaceName_UsesEmailOnly()
    {
        var (svc, handler) = CreateService();

        await svc.SendAsync("user@example.com", "   ", "Subject", "Body");

        var request = Assert.Single(handler.Requests);
        using var doc = await ReadPayloadAsync(request.Content!);
        Assert.Equal("user@example.com", doc.RootElement.GetProperty("to")[0].GetString());
    }

    [Fact]
    public async Task SendAsync_HtmlBodyWithNairaSymbol_PassesThroughUnchanged()
    {
        var (svc, handler) = CreateService();

        var html = "<p>Your balance is ₦500.50</p>";
        await svc.SendAsync("finance@acme.com", "Finance", "Low Balance", html);

        var request = Assert.Single(handler.Requests);
        using var doc = await ReadPayloadAsync(request.Content!);
        Assert.Equal(html, doc.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAsync_ApiError_ThrowsInvalidOperationExceptionWithStatusAndBody()
    {
        var (svc, handler) = CreateService(_ =>
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"message\":\"Invalid email address\"}", Encoding.UTF8, "application/json")
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SendAsync("bad@test.com", "User", "Sub", "Body"));

        Assert.Contains("422", ex.Message);
        Assert.Contains("Invalid email address", ex.Message);
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_DoesNotThrow()
    {
        var (svc, _) = CreateService();

        await svc.SendAsync("admin@acme.com", "Admin", "Subject", "<p>Body</p>");
        // No exception thrown means success
    }
}
