using Microsoft.Extensions.Logging;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Enums;

namespace TruvoID.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(
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

        return Task.CompletedTask;
    }
}
