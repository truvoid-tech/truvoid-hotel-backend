using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class NotificationPreferenceService
{
    private readonly MongoDbContext _db;

    public NotificationPreferenceService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationPreference> GetOrCreateAsync(Guid institutionId)
    {
        var prefs = await _db.NotificationPreferences
            .Find(p => p.InstitutionId == institutionId)
            .FirstOrDefaultAsync();

        if (prefs is not null) return prefs;

        prefs = new NotificationPreference
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            CreatedAt = DateTime.UtcNow
        };
        await _db.NotificationPreferences.InsertOneAsync(prefs);
        return prefs;
    }

    public async Task UpdateNotificationPrefsAsync(Guid institutionId, decimal alertThreshold, bool emailAlerts, bool smsAlerts, bool verifyEmailResults)
    {
        var prefs = await GetOrCreateAsync(institutionId);
        var update = Builders<NotificationPreference>.Update
            .Set(p => p.AlertThreshold, alertThreshold)
            .Set(p => p.EmailAlertsEnabled, emailAlerts)
            .Set(p => p.SmsAlertsEnabled, smsAlerts)
            .Set(p => p.VerifyEmailResults, verifyEmailResults)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _db.NotificationPreferences.UpdateOneAsync(p => p.Id == prefs.Id, update);
    }

    public async Task UpdateWalletAlertsAsync(Guid institutionId, decimal threshold, bool emailEnabled, bool smsEnabled)
    {
        var prefs = await GetOrCreateAsync(institutionId);
        var update = Builders<NotificationPreference>.Update
            .Set(p => p.AlertThreshold, threshold)
            .Set(p => p.EmailAlertsEnabled, emailEnabled)
            .Set(p => p.SmsAlertsEnabled, smsEnabled)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _db.NotificationPreferences.UpdateOneAsync(p => p.Id == prefs.Id, update);
    }

    public async Task UpdateBillingContactAsync(Guid institutionId, string name, string email)
    {
        var prefs = await GetOrCreateAsync(institutionId);
        var update = Builders<NotificationPreference>.Update
            .Set(p => p.BillingContactName, name)
            .Set(p => p.BillingContactEmail, email)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _db.NotificationPreferences.UpdateOneAsync(p => p.Id == prefs.Id, update);
    }
}
