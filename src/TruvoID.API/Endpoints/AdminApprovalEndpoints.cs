using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Driver;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;
using TruvoID.Infrastructure.Services;

namespace TruvoID.API.Endpoints;

public static class AdminApprovalEndpoints
{
    public static IEndpointRouteBuilder MapAdminApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        // Approve institution (Pending → Active)
        app.MapPost("/v1/admin/institutions/{id:guid}/approve", ApproveInstitution)
            .RequireAuthorization("TruvoAdmin");

        // Suspend institution (Active → Suspended)
        app.MapPost("/v1/admin/institutions/{id:guid}/suspend", SuspendInstitution)
            .RequireAuthorization("TruvoAdmin");

        // Reactivate institution (Suspended → Active)
        app.MapPost("/v1/admin/institutions/{id:guid}/reactivate", ReactivateInstitution)
            .RequireAuthorization("TruvoAdmin");

        return app;
    }

    private static async Task<IResult> ApproveInstitution(
        Guid id,
        HttpContext ctx,
        MongoDbContext db,
        INotificationService notifications,
        IAuditService audit)
    {
        var institution = await db.Institutions
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();

        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        if (institution.Status == "Active")
            return Results.Ok(new { message = "Institution is already active." });

        var update = Builders<Institution>.Update
            .Set(i => i.Status, "Active")
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        await db.Institutions.UpdateOneAsync(i => i.Id == id, update);
        await audit.LogAsync(AuditAction.Updated, nameof(Institution), id, ctx.GetUserId(), "User", $"Approved: {institution.Name}");

        // Find the admin user to send the approval email to
        var adminUser = await db.Users
            .Find(u => u.InstitutionId == id && u.Role == "Admin")
            .FirstOrDefaultAsync();

        var recipientEmail = adminUser?.Email ?? institution.ContactEmail;
        var recipientName = adminUser?.FullName ?? institution.Name;

        if (!string.IsNullOrWhiteSpace(recipientEmail))
            await notifications.SendApprovalAsync(recipientEmail, recipientName ?? string.Empty, institution.Name);

        return Results.Ok(new { message = $"{institution.Name} has been approved and activated." });
    }

    private static async Task<IResult> SuspendInstitution(
        Guid id,
        HttpContext ctx,
        MongoDbContext db,
        IAuditService audit)
    {
        var institution = await db.Institutions
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();

        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var update = Builders<Institution>.Update
            .Set(i => i.Status, "Suspended")
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        await db.Institutions.UpdateOneAsync(i => i.Id == id, update);
        await audit.LogAsync(AuditAction.Updated, nameof(Institution), id, ctx.GetUserId(), "User", $"Suspended: {institution.Name}");

        return Results.Ok(new { message = $"{institution.Name} has been suspended." });
    }

    private static async Task<IResult> ReactivateInstitution(
        Guid id,
        HttpContext ctx,
        MongoDbContext db,
        IAuditService audit)
    {
        var institution = await db.Institutions
            .Find(i => i.Id == id)
            .FirstOrDefaultAsync();

        if (institution is null)
            return Results.NotFound(new { error = "Institution not found." });

        var update = Builders<Institution>.Update
            .Set(i => i.Status, "Active")
            .Set(i => i.UpdatedAt, DateTime.UtcNow);
        await db.Institutions.UpdateOneAsync(i => i.Id == id, update);
        await audit.LogAsync(AuditAction.Updated, nameof(Institution), id, ctx.GetUserId(), "User", $"Reactivated: {institution.Name}");

        return Results.Ok(new { message = $"{institution.Name} has been reactivated." });
    }
}
