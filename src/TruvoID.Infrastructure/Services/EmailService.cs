using System.Net.Http.Json;
using System.Text.Json;

namespace TruvoID.Infrastructure.Services;

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

        var response = await _http.PostAsJsonAsync("https://api.resend.com/emails", payload, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Resend API error {(int)response.StatusCode}: {error}");
        }
    }
}
