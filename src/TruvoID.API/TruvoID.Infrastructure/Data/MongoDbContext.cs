using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data;

// ── Wallet entities (used by WalletEndpoints) ──────────────────────────────

[BsonIgnoreExtraElements]
public class WalletLedger
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string Type { get; set; } = ""; // Credit, Debit
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class WalletTopUp
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string PaymentMethod { get; set; } = "manual";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
}

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<Institution> Institutions =>
        _database.GetCollection<Institution>("institutions");

    public IMongoCollection<User> Users =>
        _database.GetCollection<User>("users");

    public IMongoCollection<VerificationCall> VerificationCalls =>
        _database.GetCollection<VerificationCall>("verification_calls");

    public IMongoCollection<NotificationPreference> NotificationPreferences =>
        _database.GetCollection<NotificationPreference>("notification_preferences");

    public IMongoCollection<NotificationEvent> NotificationEvents =>
        _database.GetCollection<NotificationEvent>("notification_events");

    public IMongoCollection<InstitutionPricing> InstitutionPricings =>
        _database.GetCollection<InstitutionPricing>("institution_pricings");

    public IMongoCollection<NimcConfig> NimcConfigs =>
        _database.GetCollection<NimcConfig>("nimc_configs");

    public IMongoCollection<WalletLedger> WalletLedgers =>
        _database.GetCollection<WalletLedger>("wallet_ledgers");

    public IMongoCollection<WalletTopUp> WalletTopUps =>
        _database.GetCollection<WalletTopUp>("wallet_topups");
}
