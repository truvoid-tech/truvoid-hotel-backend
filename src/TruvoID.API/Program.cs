using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TruvoID.API.Extensions;
using TruvoID.API.Middleware;
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

// CORS for dashboard SPA and external API consumers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Global exception handler — log errors instead of swallowing them
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        Console.WriteLine($"[ERROR] {exception?.Message}");
        Console.WriteLine($"[ERROR] {exception?.StackTrace}");

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

// JWT authentication — must come before authorization
app.UseAuthentication();
app.UseAuthorization();

// API key authentication (applies to /v1/* endpoints for API gateway consumers)
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthenticationMiddleware>());

app.MapControllers();

// ─── Seed database on startup ───
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TruvoIDDbContext>();

    // Try migration first, fall back to EnsureCreated if migrations are missing
    try
    {
        await db.Database.MigrateAsync();
        Console.WriteLine("[Startup] Database migrated successfully.");
    }
    catch (Exception migrateEx)
    {
        Console.WriteLine($"[Startup] MigrateAsync failed ({migrateEx.Message}). Trying EnsureCreated...");

        // EnsureCreated creates the schema from the current model — no migration files needed
        // Downside: won't apply future migrations, but works for initial setup
        await db.Database.EnsureCreatedAsync();
        Console.WriteLine("[Startup] Database created/verified via EnsureCreated.");
    }

    await SeedData.SeedAsync(db);
    Console.WriteLine("[Startup] Database seeded successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database setup FAILED: {ex.Message}");
    Console.WriteLine($"[Startup] Stack trace: {ex.StackTrace}");
    Console.WriteLine("[Startup] The API will start anyway — database operations will fail until the DB is accessible.");
}

Console.WriteLine($"[Startup] TruvoID API listening on port {port}");

app.Run();
