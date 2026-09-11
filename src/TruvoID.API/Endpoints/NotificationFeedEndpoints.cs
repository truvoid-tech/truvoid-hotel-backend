using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Endpoints;

public static class NotificationFeedEndpoints
{
    public static IEndpointRouteBuilder MapNotificationFeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notifications")
            .RequireAuthorization();

        group.MapGet("/", GetNotifications);
        group.MapGet("/unread-count", GetUnreadCount);
        group.MapPost("/{id}/read", MarkNotificationRead);
        group.MapPost("/read-all", MarkAllNotificationsRead);

        return app;
    }

    private static async Task<IResult> GetNotifications(
        HttpContext ctx,
        NotificationFeedService feed)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var events = await feed.GetRecentAsync(institutionId, 50);

        return Results.Ok(events.Select(e => new
        {
            id = e.Id,
            category = e.Category,
            title = e.Title,
            message = e.Message,
            actionUrl = e.ActionUrl,
            isRead = e.IsRead,
            createdAt = e.CreatedAt,
            readAt = e.ReadAt
        }));
    }

    private static async Task<IResult> GetUnreadCount(
        HttpContext ctx,
        NotificationFeedService feed)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        var count = await feed.GetUnreadCountAsync(institutionId);
        return Results.Ok(new { count });
    }

    private static async Task<IResult> MarkNotificationRead(
        string id,
        HttpContext ctx,
        NotificationFeedService feed)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        await feed.MarkReadAsync(institutionId, id);
        return Results.Ok(new { message = "Notification marked as read." });
    }

    private static async Task<IResult> MarkAllNotificationsRead(
        HttpContext ctx,
        NotificationFeedService feed)
    {
        var institutionId = ctx.GetInstitutionId();
        if (institutionId == Guid.Empty) return Results.Unauthorized();

        await feed.MarkAllReadAsync(institutionId);
        return Results.Ok(new { message = "All notifications marked as read." });
    }
}
