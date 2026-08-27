using TruvoID.Domain.Enums;

namespace TruvoID.Core.Interfaces;

/// <summary>
/// Audit service for immutable compliance logging.
/// Records every verification call and wallet transaction.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        AuditAction action,
        string entity,
        Guid entityId,
        Guid? actorId = null,
        string? actorType = null,
        string? detailsJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);
}
