using MongoDB.Driver;
using StackExchange.Redis;
using TruvoID.Core.Interfaces;
using TruvoID.Infrastructure.Data;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTruvoIDServices(this IServiceCollection services, IConfiguration configuration)
    {
        // MongoDB
        var mongoConnectionString = configuration.GetConnectionString("MongoDb")
            ?? Environment.GetEnvironmentVariable("MONGO_URI")
            ?? "mongodb://localhost:27017";
        var mongoDatabaseName = configuration["MongoDatabase"] ?? "truvoid";

        Console.WriteLine($"[Startup] Connecting to MongoDB: {mongoConnectionString.Split('@').LastOrDefault() ?? mongoConnectionString}");

        var mongoClient = new MongoClient(mongoConnectionString);
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton(sp => new MongoDbContext(mongoClient, mongoDatabaseName));

        // Redis — only if configured
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
}
