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

// ─── Ensure database schema exists on startup ───
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TruvoIDDbContext>();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();

    // Check if the users table exists
    using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users'";
        var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);

        if (count == 0)
        {
            Console.WriteLine("[Startup] Users table not found. Dropping __EFMigrationsHistory and creating schema...");

            // Drop the stale migration history so EnsureCreated works
            using (var dropCmd = conn.CreateCommand())
            {
                dropCmd.CommandText = "DROP TABLE IF EXISTS \"__EFMigrationsHistory\" CASCADE";
                await dropCmd.ExecuteNonQueryAsync();
            }

            // Create all tables from the DbContext model
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("[Startup] Schema created via EnsureCreated.");
        }
        else
        {
            Console.WriteLine("[Startup] Database schema already exists — skipping.");
        }
    }

    await SeedData.SeedAsync(db);
    Console.WriteLine("[Startup] Database seeded successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database setup failed: {ex.Message}");
    Console.WriteLine($"[Startup] Stack trace: {ex.StackTrace}");
}

Console.WriteLine($"[Startup] TruvoID API listening on port {port}");

app.Run();
