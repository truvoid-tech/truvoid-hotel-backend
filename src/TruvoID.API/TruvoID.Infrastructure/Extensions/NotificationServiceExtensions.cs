using Microsoft.Extensions.DependencyInjection;
using TruvoID.Infrastructure.Services;

namespace TruvoID.Infrastructure.Extensions;

public static class NotificationServiceExtensions
{
    /// <summary>
    /// Registers the Resend email service and notification services.
    /// Requires RESEND_API_KEY in configuration/environment.
    /// Call after AddHttpClient() is available in the service collection.
    /// </summary>
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        string resendApiKey)
    {
        // Register the named HttpClient for Resend
        services.AddHttpClient("resend", client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {resendApiKey}");
        });

        // Email + notification services
        services.AddScoped<IEmailService, ResendEmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationPreferenceService>();

        return services;
    }
}
