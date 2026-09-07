using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditAction action, string entity, Guid entityId, Guid? actorId = null, string? actorType = null, string? details = null, CancellationToken ct = default);
}
