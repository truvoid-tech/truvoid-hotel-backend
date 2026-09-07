using Microsoft.AspNetCore.Routing;

namespace TruvoID.API.Endpoints;

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapTruvoIdEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapNotificationEndpoints();
        app.MapNotificationFeedEndpoints();
        app.MapWalletAlertEndpoints();
        app.MapPasswordResetEndpoints();
        app.MapAdminApprovalEndpoints();
        app.MapPricingEndpoints();
        app.MapWalletEndpoints();

        return app;
    }
}
