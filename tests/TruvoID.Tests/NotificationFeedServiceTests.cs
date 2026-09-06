using MongoDB.Bson;
using TruvoID.Domain.Entities;

namespace TruvoID.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// In-Memory NotificationFeedService (mirrors the real one but uses List<T>)
// ══════════════════════════════════════════════════════════════════════════════

public class InMemoryNotificationFeedService
{
    private readonly List<NotificationEvent> _store = new();

    public Task PushAsync(Guid institutionId, string category, string title, string message, string? actionUrl = null, CancellationToken ct = default)
    {
        _store.Add(new NotificationEvent
        {
            Id = ObjectId.GenerateNewId().ToString(),
            InstitutionId = institutionId,
            Category = category,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task<List<NotificationEvent>> GetRecentAsync(Guid institutionId, int limit = 20, CancellationToken ct = default)
    {
        var results = _store
            .Where(e => e.InstitutionId == institutionId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<int> GetUnreadCountAsync(Guid institutionId, CancellationToken ct = default)
    {
        var count = _store.Count(e => e.InstitutionId == institutionId && !e.IsRead);
        return Task.FromResult(count);
    }

    public Task MarkReadAsync(string notificationId, CancellationToken ct = default)
    {
        var evt = _store.FirstOrDefault(e => e.Id == notificationId);
        if (evt != null)
        {
            evt.IsRead = true;
            evt.ReadAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task MarkAllReadAsync(Guid institutionId, CancellationToken ct = default)
    {
        foreach (var evt in _store.Where(e => e.InstitutionId == institutionId && !e.IsRead))
        {
            evt.IsRead = true;
            evt.ReadAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Tests
// ══════════════════════════════════════════════════════════════════════════════

public class NotificationFeedServiceTests
{
    private readonly InMemoryNotificationFeedService _svc = new();
    private readonly Guid _instA = Guid.NewGuid();
    private readonly Guid _instB = Guid.NewGuid();

    // ── PushAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushAsync_CreatesNotificationWithCorrectFields()
    {
        await _svc.PushAsync(_instA, "low_balance", "Low Balance", "Your wallet is running low", "/wallet");

        var results = await _svc.GetRecentAsync(_instA);
        Assert.Single(results);
        Assert.Equal("low_balance", results[0].Category);
        Assert.Equal("Low Balance", results[0].Title);
        Assert.Equal("Your wallet is running low", results[0].Message);
        Assert.Equal("/wallet", results[0].ActionUrl);
        Assert.Equal(_instA, results[0].InstitutionId);
        Assert.False(results[0].IsRead);
    }

    [Fact]
    public async Task PushAsync_MultipleNotifications_AreStored()
    {
        await _svc.PushAsync(_instA, "low_balance", "Low Balance", "msg1");
        await _svc.PushAsync(_instA, "verification_result", "Verification Complete", "msg2");
        await _svc.PushAsync(_instA, "approval", "Approved", "msg3");

        var results = await _svc.GetRecentAsync(_instA);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task PushAsync_NullActionUrl_IsAllowed()
    {
        await _svc.PushAsync(_instA, "approval", "Approved", "msg", null);

        var results = await _svc.GetRecentAsync(_instA);
        Assert.Single(results);
        Assert.Null(results[0].ActionUrl);
    }

    // ── GetRecentAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        await _svc.PushAsync(_instA, "c", "First", "msg1");
        await Task.Delay(10); // ensure different timestamps
        await _svc.PushAsync(_instA, "c", "Second", "msg2");
        await Task.Delay(10);
        await _svc.PushAsync(_instA, "c", "Third", "msg3");

        var results = await _svc.GetRecentAsync(_instA);
        Assert.Equal(3, results.Count);
        Assert.Equal("Third", results[0].Title);
        Assert.Equal("Second", results[1].Title);
        Assert.Equal("First", results[2].Title);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsLimit()
    {
        for (var i = 0; i < 10; i++)
            await _svc.PushAsync(_instA, "c", $"Notif {i}", "msg");

        var results = await _svc.GetRecentAsync(_instA, limit: 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetRecentAsync_IsolatesByInstitution()
    {
        await _svc.PushAsync(_instA, "c", "A's notification", "msg");
        await _svc.PushAsync(_instB, "c", "B's notification", "msg");

        var aResults = await _svc.GetRecentAsync(_instA);
        var bResults = await _svc.GetRecentAsync(_instB);

        Assert.Single(aResults);
        Assert.Equal("A's notification", aResults[0].Title);
        Assert.Single(bResults);
        Assert.Equal("B's notification", bResults[0].Title);
    }

    [Fact]
    public async Task GetRecentAsync_EmptyWhenNoNotifications()
    {
        var results = await _svc.GetRecentAsync(_instA);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetRecentAsync_EmptyWhenWrongInstitution()
    {
        await _svc.PushAsync(_instA, "c", "A's", "msg");
        var results = await _svc.GetRecentAsync(Guid.NewGuid());
        Assert.Empty(results);
    }

    // ── GetUnreadCountAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsZero_WhenEmpty()
    {
        var count = await _svc.GetUnreadCountAsync(_instA);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnread()
    {
        await _svc.PushAsync(_instA, "c", "1", "msg");
        await _svc.PushAsync(_instA, "c", "2", "msg");
        await _svc.PushAsync(_instA, "c", "3", "msg");

        // Mark one as read
        var all = await _svc.GetRecentAsync(_instA);
        await _svc.MarkReadAsync(all[0].Id);

        var count = await _svc.GetUnreadCountAsync(_instA);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetUnreadCountAsync_IsolatesByInstitution()
    {
        await _svc.PushAsync(_instA, "c", "A1", "msg");
        await _svc.PushAsync(_instA, "c", "A2", "msg");
        await _svc.PushAsync(_instB, "c", "B1", "msg");

        Assert.Equal(2, await _svc.GetUnreadCountAsync(_instA));
        Assert.Equal(1, await _svc.GetUnreadCountAsync(_instB));
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsZero_AfterMarkAllRead()
    {
        await _svc.PushAsync(_instA, "c", "1", "msg");
        await _svc.PushAsync(_instA, "c", "2", "msg");

        await _svc.MarkAllReadAsync(_instA);

        Assert.Equal(0, await _svc.GetUnreadCountAsync(_instA));
    }

    // ── MarkReadAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkReadAsync_SetsIsReadAndReadAt()
    {
        await _svc.PushAsync(_instA, "c", "Test", "msg");
        var all = await _svc.GetRecentAsync(_instA);
        var id = all[0].Id;

        Assert.False(all[0].IsRead);
        Assert.Null(all[0].ReadAt);

        await _svc.MarkReadAsync(id);

        var updated = await _svc.GetRecentAsync(_instA);
        Assert.True(updated[0].IsRead);
        Assert.NotNull(updated[0].ReadAt);
    }

    [Fact]
    public async Task MarkReadAsync_NonExistentId_DoesNotThrow()
    {
        await _svc.MarkReadAsync("nonexistent_id_123");
        // No exception thrown
        Assert.Equal(0, await _svc.GetUnreadCountAsync(_instA));
    }

    // ── MarkAllReadAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllReadAsync_MarksAllNotificationsForInstitution()
    {
        await _svc.PushAsync(_instA, "c", "1", "msg");
        await _svc.PushAsync(_instA, "c", "2", "msg");
        await _svc.PushAsync(_instA, "c", "3", "msg");

        await _svc.MarkAllReadAsync(_instA);

        var results = await _svc.GetRecentAsync(_instA);
        Assert.All(results, r => Assert.True(r.IsRead));
        Assert.All(results, r => Assert.NotNull(r.ReadAt));
    }

    [Fact]
    public async Task MarkAllReadAsync_DoesNotAffectOtherInstitutions()
    {
        await _svc.PushAsync(_instA, "c", "A1", "msg");
        await _svc.PushAsync(_instB, "c", "B1", "msg");

        await _svc.MarkAllReadAsync(_instA);

        Assert.Equal(0, await _svc.GetUnreadCountAsync(_instA));
        Assert.Equal(1, await _svc.GetUnreadCountAsync(_instB));
    }

    [Fact]
    public async Task MarkAllReadAsync_AlreadyAllRead_NoError()
    {
        await _svc.PushAsync(_instA, "c", "1", "msg");
        await _svc.MarkAllReadAsync(_instA);
        await _svc.MarkAllReadAsync(_instA); // second call

        Assert.Equal(0, await _svc.GetUnreadCountAsync(_instA));
    }

    [Fact]
    public async Task MarkAllReadAsync_EmptyStore_NoError()
    {
        await _svc.MarkAllReadAsync(_instA);
        Assert.Equal(0, await _svc.GetUnreadCountAsync(_instA));
    }

    // ── Full Lifecycle Test ──────────────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_PushReadCountMarkRead()
    {
        // 1. Push 3 notifications
        await _svc.PushAsync(_instA, "low_balance", "Balance Low", "Your balance is ₦500", "/wallet");
        await _svc.PushAsync(_instA, "verification_result", "NIN Verified", "Result: Match", "/history");
        await _svc.PushAsync(_instA, "approval", "Approved", "Account approved", "/dashboard");

        // 2. All unread
        Assert.Equal(3, await _svc.GetUnreadCountAsync(_instA));

        // 3. Mark first as read
        var all = await _svc.GetRecentAsync(_instA);
        await _svc.MarkReadAsync(all[0].Id);
        Assert.Equal(2, await _svc.GetUnreadCountAsync(_instA));

        // 4. Mark all remaining as read
        await _svc.MarkAllReadAsync(_instA);
        Assert.Equal(0, await _svc.GetUnreadCountAsync(_instA));

        // 5. Verify all have readAt set
        var final = await _svc.GetRecentAsync(_instA);
        Assert.All(final, r =>
        {
            Assert.True(r.IsRead);
            Assert.NotNull(r.ReadAt);
        });
    }

    // ── Category Diversity ───────────────────────────────────────────────────

    [Fact]
    public async Task PushAsync_AllCategories_AreStored()
    {
        var categories = new[] { "low_balance", "verification_result", "approval", "payment", "staff_invitation" };
        foreach (var cat in categories)
            await _svc.PushAsync(_instA, cat, $"{cat} title", $"{cat} message");

        var results = await _svc.GetRecentAsync(_instA, 10);
        Assert.Equal(categories.Length, results.Count);

        foreach (var cat in categories)
            Assert.Contains(results, r => r.Category == cat);
    }
}
