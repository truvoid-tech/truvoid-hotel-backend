using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly TruvoIDDbContext _db;

    public AuditService(TruvoIDDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        AuditAction action,
        string entity,
        Guid entityId,
        Guid? actorId = null,
        string? actorType = null,
        string? detailsJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            ActorId = actorId,
            ActorType = actorType ?? string.Empty,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            DetailsJson = detailsJson,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
