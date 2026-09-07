using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class NotificationFeedService
{
    private readonly MongoDbContext _db;

    public NotificationFeedService(MongoDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Push a new notification event for an institution.
    /// </summary>
    public async Task PushAsync(Guid institutionId, string category, string title, string message, string? actionUrl = null, CancellationToken ct = default)
    {
        var evt = new NotificationEvent
        {
            InstitutionId = institutionId,
            Category = category,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        await _db.NotificationEvents.InsertOneAsync(evt, cancellationToken: ct);
    }

    /// <summary>
    /// Get recent notifications for an institution (newest first).
    /// </summary>
    public async Task<List<NotificationEvent>> GetRecentAsync(Guid institutionId, int limit = 20, CancellationToken ct = default)
    {
        return await _db.NotificationEvents
            .Find(e => e.InstitutionId == institutionId)
            .SortByDescending(e => e.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Get unread count for an institution.
    /// </summary>
    public async Task<int> GetUnreadCountAsync(Guid institutionId, CancellationToken ct = default)
    {
        return (int)await _db.NotificationEvents
            .CountDocumentsAsync(e => e.InstitutionId == institutionId && !e.IsRead, cancellationToken: ct);
    }

    /// <summary>
    /// Mark a single notification as read.
    /// </summary>
    public async Task MarkReadAsync(string notificationId, CancellationToken ct = default)
    {
        var update = Builders<NotificationEvent>.Update
            .Set(e => e.IsRead, true)
            .Set(e => e.ReadAt, DateTime.UtcNow);
        await _db.NotificationEvents.UpdateOneAsync(
            e => e.Id == notificationId, update, cancellationToken: ct);
    }

    /// <summary>
    /// Mark all notifications for an institution as read.
    /// </summary>
    public async Task MarkAllReadAsync(Guid institutionId, CancellationToken ct = default)
    {
        var update = Builders<NotificationEvent>.Update
            .Set(e => e.IsRead, true)
            .Set(e => e.ReadAt, DateTime.UtcNow);
        await _db.NotificationEvents.UpdateManyAsync(
            e => e.InstitutionId == institutionId && !e.IsRead, update, cancellationToken: ct);
    }
}
