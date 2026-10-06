using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TruvoID.Infrastructure.Postgres;

namespace TruvoID.API.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notifications").RequireAuthorization();
        group.MapGet("/", List);
        group.MapPost("/{id:guid}/read", MarkRead);
        group.MapPost("/read-all", MarkAllRead);
        return app;
    }

    private static async Task<IResult> List(
        HttpContext ctx, NotificationStore notifications, bool unreadOnly = false, int limit = 20, CancellationToken ct = default)
    {
        var userId = ctx.GetUserId();
        if (userId == Guid.Empty) return Results.Unauthorized();
        var items = await notifications.ListAsync(userId, unreadOnly, limit, ct);
        var unread = await notifications.UnreadCountAsync(userId, ct);
        return Results.Ok(new { unread, items });
    }

    private static async Task<IResult> MarkRead(HttpContext ctx, Guid id, NotificationStore notifications, CancellationToken ct)
    {
        var userId = ctx.GetUserId();
        if (userId == Guid.Empty) return Results.Unauthorized();
        await notifications.MarkReadAsync(userId, id, ct);
        return Results.Ok(new { unread = await notifications.UnreadCountAsync(userId, ct) });
    }

    private static async Task<IResult> MarkAllRead(HttpContext ctx, NotificationStore notifications, CancellationToken ct)
    {
        var userId = ctx.GetUserId();
        if (userId == Guid.Empty) return Results.Unauthorized();
        await notifications.MarkAllReadAsync(userId, ct);
        return Results.Ok(new { unread = 0 });
    }
}
