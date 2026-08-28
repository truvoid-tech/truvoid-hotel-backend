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
        // PostgreSQL
        services.AddDbContext<TruvoIDDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Redis — only connect if a real (non-localhost) URL is provided
        // In Railway, set ConnectionStrings__Redis or leave it empty to skip
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
            Console.WriteLine("[Startup] Redis not configured or is localhost — skipping. Rate limiting and balance cache disabled.");
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
}
