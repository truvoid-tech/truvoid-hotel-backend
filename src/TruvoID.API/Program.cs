using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using TruvoID.API.Endpoints;
using TruvoID.Core.Interfaces;
using TruvoID.Infrastructure.Data;
using TruvoID.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Port / hosting ─────────────────────────────────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://+:{port}");

// ── Configuration ─────────────────────────────────────────────────────────
var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
    ?? "mongodb://localhost:27017";
var mongoDatabase = builder.Configuration["MongoDb:Database"]
    ?? Environment.GetEnvironmentVariable("MONGO_DATABASE")
    ?? "truvoid";

// ── MongoDB ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<MongoDbContext>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return new MongoDbContext(client, mongoDatabase);
});

// ── JWT auth ──────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "dev-secret-key-change-in-production-32chars!!!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = "TruvoID",
            ValidateAudience = true,
            ValidAudience = "TruvoID",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TruvoAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Admin", "SuperAdmin", "PlatformAdmin"));
});

// ── HTTP clients for upstream services ────────────────────────────────────
builder.Services.AddHttpClient("idaccess", client =>
{
    client.BaseAddress = new Uri("https://idaccess.info/v1/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddHttpClient("resend", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ── Application services ──────────────────────────────────────────────────
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<INimcConfigService, NimcConfigService>();
builder.Services.AddScoped<NotificationFeedService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Mailer: swap ResendEmailService for a no-op dev implementation if you
// set NOTIFICATIONS_DISABLED=1 so local dev doesn't blow up on missing API keys.
if (Environment.GetEnvironmentVariable("NOTIFICATIONS_DISABLED") == "1")
{
    builder.Services.AddScoped<IEmailService, DevNullEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, ResendEmailService>();
}

// ── Build & map endpoints ─────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapTruvoIdEndpoints();

app.Run();

// ── Dev-only no-op mailer ─────────────────────────────────────────────────
public class DevNullEmailService : IEmailService
{
    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        Console.WriteLine($"[DEV NULL EMAIL] Would send to {toEmail}: {subject}");
        return Task.CompletedTask;
    }
}
