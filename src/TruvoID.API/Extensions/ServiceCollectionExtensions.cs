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

        // Redis
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
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
