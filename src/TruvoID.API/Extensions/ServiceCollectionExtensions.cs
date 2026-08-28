using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TruvoID.Core.Interfaces;
using TruvoID.Infrastructure.Data;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services (DbContext, Redis, business services).
    /// </summary>
    public static IServiceCollection AddTruvoIDServices(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL — auto-append Ssl Mode=Require for Neon if not already present
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        if (!connectionString.Contains("Ssl Mode", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += ";Ssl Mode=Require";
            Console.WriteLine("[Startup] Auto-appended Ssl Mode=Require to connection string for Neon compatibility.");
        }

        Console.WriteLine($"[Startup] Connecting to database: {ExtractHost(connectionString)}");

        services.AddDbContext<TruvoIDDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Redis — only connect if a real (non-localhost) URL is provided
        var redisConnection = configuration.GetConnectionString("Redis");
        var isRedisConfigured = !string.IsNullOrWhiteSpace(redisConnection)
                                && !redisConnection.Contains("localhost", StringComparison.OrdinalIgnoreCase);

        if (isRedisConfigured)
        {
            try
            {
                var mux = ConnectionMultiplexer.Connect(redisConnection!);
                services.AddSingleton<IConnectionMultiplexer>(mux);
                Console.WriteLine("[Startup] Redis connected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] Redis connection failed: {ex.Message}. Continuing without Redis.");
            }
        }
        else
        {
            Console.WriteLine("[Startup] Redis not configured or is localhost — skipping.");
        }

        // Business services
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ICallHistoryService, CallHistoryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }

    private static string ExtractHost(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
                return trimmed;
        }
        return "(host not found in connection string)";
    }
}
