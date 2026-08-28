using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using TruvoID.API.Extensions;
using TruvoID.Components;
using TruvoID.Components.Services;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway binds to the PORT env var
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "TruvoID",
        ValidAudience = jwtSettings["Audience"] ?? "TruvoID",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// Blazor auth services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<TruvoIDAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TruvoIDAuthStateProvider>());
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("/") });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add TruvoID services (MongoDB, Redis, business services)
builder.Services.AddTruvoIDServices(builder.Configuration);

var app = builder.Build();

// ─── MongoDB: Create indexes + seed data on startup ───
try
{
    var mongoDbContext = app.Services.GetRequiredService<MongoDbContext>();

    Console.WriteLine("[Startup] Ensuring MongoDB indexes...");
    await mongoDbContext.EnsureIndexesAsync();
    Console.WriteLine("[Startup] MongoDB indexes ready.");

    var hasAdmin = await mongoDbContext.Users.CountDocumentsAsync(u => u.Role == UserRole.PlatformAdmin) > 0;
    if (!hasAdmin)
    {
        Console.WriteLine("[Startup] Seeding PlatformAdmin user...");
        await mongoDbContext.Users.InsertOneAsync(new User
        {
            Email = "admin@truvoid.ng",
            FullName = "TruvoID Platform Admin",
            PhoneNumber = "+2348000000000",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
            Role = UserRole.PlatformAdmin,
            Status = UserStatus.Active
        });
        Console.WriteLine("[Seed] PlatformAdmin created: admin@truvoid.ng / Admin@12345");
    }

    var hasPricing = await mongoDbContext.PricingRates.CountDocumentsAsync(_ => true) > 0;
    if (!hasPricing)
    {
        Console.WriteLine("[Startup] Seeding default pricing rates...");
        await mongoDbContext.PricingRates.InsertManyAsync(new List<PricingRate>
        {
            new() { Type = VerificationType.Nin, PricePerCall = 100m, NimcPartnerCost = 45m, IsActive = true, EffectiveFrom = DateTime.UtcNow },
            new() { Type = VerificationType.Bvn, PricePerCall = 150m, NimcPartnerCost = 65m, IsActive = true, EffectiveFrom = DateTime.UtcNow },
            new() { Type = VerificationType.Phone, PricePerCall = 50m, NimcPartnerCost = 20m, IsActive = true, EffectiveFrom = DateTime.UtcNow }
        });
        Console.WriteLine("[Seed] Default pricing rates created.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database setup failed: {ex.Message}");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine($"[Startup] TruvoID listening on port {port}");

app.Run();
