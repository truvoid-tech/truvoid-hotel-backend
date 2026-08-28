using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using TruvoID.API.Extensions;
using TruvoID.API.Middleware;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway binds to the PORT env var
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// Add services
builder.Services.AddTruvoIDServices(builder.Configuration);

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey))
{
    Console.WriteLine("[FATAL] Jwt__SecretKey environment variable is not set. The API cannot start.");
    throw new InvalidOperationException("JWT SecretKey not configured. Set the Jwt__SecretKey environment variable.");
}
var key = Encoding.UTF8.GetBytes(secretKey);

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
        ValidIssuer = jwtSection["Issuer"] ?? "TruvoID",
        ValidAudience = jwtSection["Audience"] ?? "TruvoID",
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        Console.WriteLine($"[ERROR] {exception?.Message}");

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "error",
            code = 500,
            message = exception?.Message ?? "An unexpected error occurred."
        });
    });
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthenticationMiddleware>());

app.MapControllers();

// ─── MongoDB: Create indexes + seed data on startup ───
try
{
    var mongoDbContext = app.Services.GetRequiredService<MongoDbContext>();

    Console.WriteLine("[Startup] Ensuring MongoDB indexes...");
    await mongoDbContext.EnsureIndexesAsync();
    Console.WriteLine("[Startup] MongoDB indexes ready.");

    // Seed PlatformAdmin user
    var hasAdmin = await mongoDbContext.Users.CountDocumentsAsync(u => u.Role == UserRole.PlatformAdmin) > 0;
    if (!hasAdmin)
    {
        Console.WriteLine("[Startup] Seeding PlatformAdmin user...");
        var adminUser = new User
        {
            Email = "admin@truvoid.ng",
            FullName = "TruvoID Platform Admin",
            PhoneNumber = "+2348000000000",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
            Role = UserRole.PlatformAdmin,
            Status = UserStatus.Active,
            DailyCallLimit = null
        };
        await mongoDbContext.Users.InsertOneAsync(adminUser);
        Console.WriteLine("[Seed] PlatformAdmin created: admin@truvoid.ng / Admin@12345");
    }

    // Seed default pricing rates
    var hasPricing = await mongoDbContext.PricingRates.CountDocumentsAsync(_ => true) > 0;
    if (!hasPricing)
    {
        Console.WriteLine("[Startup] Seeding default pricing rates...");
        var defaultRates = new List<PricingRate>
        {
            new() { Type = VerificationType.Nin, PricePerCall = 100.00m, NimcPartnerCost = 45.00m, IsActive = true, EffectiveFrom = DateTime.UtcNow },
            new() { Type = VerificationType.Bvn, PricePerCall = 150.00m, NimcPartnerCost = 65.00m, IsActive = true, EffectiveFrom = DateTime.UtcNow },
            new() { Type = VerificationType.Phone, PricePerCall = 50.00m, NimcPartnerCost = 20.00m, IsActive = true, EffectiveFrom = DateTime.UtcNow }
        };
        await mongoDbContext.PricingRates.InsertManyAsync(defaultRates);
        Console.WriteLine("[Seed] Default pricing rates created.");
    }

    Console.WriteLine("[Startup] Database seeded successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database setup failed: {ex.Message}");
    Console.WriteLine($"[Startup] Stack trace: {ex.StackTrace}");
}

Console.WriteLine($"[Startup] TruvoID API listening on port {port}");

app.Run();
