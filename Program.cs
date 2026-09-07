using Microsoft.AspNetCore.Components.Authorization;
using TruvoID.Components;
using TruvoID.Components.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway / Cloud: bind to PORT env var
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Blazor auth services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<TruvoIDAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TruvoIDAuthStateProvider>());

// HttpClient points to the separate BE API service
var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";

// A malformed API_BASE_URL used to crash every single page (Uri construction ran
// inside a DI factory resolved on nearly every component). Log the exact raw
// value so a bad env var is diagnosable instead of a bare UriFormatException,
// and fall back instead of taking the whole site down.
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
{
    Console.WriteLine($"[STARTUP] API_BASE_URL is not a valid absolute URI: \"{apiBaseUrl}\" (length {apiBaseUrl.Length}). Falling back to https://api.gettruvoid.com");
    apiBaseUri = new Uri("https://api.gettruvoid.com");
}
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseUri });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// TEMPORARY diagnostic route — uses the exact same HttpClient the app makes real
// API calls with, so it reproduces whatever is causing "Unable to connect" instead
// of guessing from outside the container. Remove once the connectivity issue is found.
app.MapGet("/diag/api-check", async (HttpClient http) =>
{
    var result = new Dictionary<string, object?> { ["baseAddress"] = http.BaseAddress?.ToString() };
    try
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resp = await http.GetAsync("/v1/auth/me");
        sw.Stop();
        var body = await resp.Content.ReadAsStringAsync();
        result["elapsedMs"] = sw.ElapsedMilliseconds;
        result["statusCode"] = (int)resp.StatusCode;
        result["body"] = body.Length > 300 ? body[..300] : body;
    }
    catch (Exception ex)
    {
        result["exceptionType"] = ex.GetType().FullName;
        result["message"] = ex.Message;
        result["innerExceptionType"] = ex.InnerException?.GetType().FullName;
        result["innerMessage"] = ex.InnerException?.Message;
    }
    return Results.Json(result);
});

app.Run();
