using Microsoft.Extensions.Logging;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly MongoDbContext _db;

    public AuditService(ILogger<AuditService> logger, MongoDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task LogAsync(
        AuditAction action,
        string entity,
        Guid entityId,
        Guid? actorId = null,
        string? actorType = null,
        string? details = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AUDIT: {Action} {Entity} {EntityId} by {ActorType} {ActorId} {Details}",
            action,
            entity,
            entityId,
            actorType ?? "-",
            actorId?.ToString() ?? "-",
            details ?? "-");

        try
        {
            await _db.AuditLogs.InsertOneAsync(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                ActorId = actorId,
                ActorType = actorType,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                DetailsJson = details,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Never let audit logging itself break the calling operation.
            _logger.LogError(ex, "Failed to persist audit log entry for {Action} {Entity} {EntityId}", action, entity, entityId);
        }
    }
}
