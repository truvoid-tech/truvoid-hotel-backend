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

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// NOTE: Do NOT use UseHttpsRedirection() — Railway terminates TLS at the edge
// and forwards plain HTTP to the container. HTTPS redirect causes an infinite loop.

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
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
    Console.WriteLine("[Startup] Database migrated and seeded successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database migration/seed failed: {ex.Message}");
    Console.WriteLine("[Startup] The API will start anyway — database operations will fail until the DB is accessible.");
}

Console.WriteLine($"[Startup] TruvoID API listening on port {port}");

app.Run();
