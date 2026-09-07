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

app.Run();
