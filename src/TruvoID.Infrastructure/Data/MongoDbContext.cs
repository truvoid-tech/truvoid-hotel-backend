using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    static MongoDbContext()
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<Institution> Institutions => _database.GetCollection<Institution>("institutions");
    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<ApiKey> ApiKeys => _database.GetCollection<ApiKey>("api_keys");
    public IMongoCollection<WalletLedgerEntry> WalletLedgerEntries => _database.GetCollection<WalletLedgerEntry>("wallet_ledger_entries");
    public IMongoCollection<VerificationCall> VerificationCalls => _database.GetCollection<VerificationCall>("verification_calls");
    public IMongoCollection<AuditLog> AuditLogs => _database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<PricingRate> PricingRates => _database.GetCollection<PricingRate>("pricing_rates");
    public IMongoCollection<RefreshToken> RefreshTokens => _database.GetCollection<RefreshToken>("refresh_tokens");
    public IMongoCollection<NimcConfig> NimcConfigs => _database.GetCollection<NimcConfig>("nimc_configs");

    public async Task EnsureIndexesAsync()
    {
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true, Name = "ix_users_email" }));

        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.InstitutionId),
            new CreateIndexOptions { Name = "ix_users_institution" }));

        await Institutions.Indexes.CreateOneAsync(new CreateIndexModel<Institution>(
            Builders<Institution>.IndexKeys.Ascending(i => i.Name),
            new CreateIndexOptions { Unique = true, Name = "ix_institutions_name" }));

        await ApiKeys.Indexes.CreateOneAsync(new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(k => k.KeyHash),
            new CreateIndexOptions { Unique = true, Name = "ix_apikeys_hash" }));

        await ApiKeys.Indexes.CreateOneAsync(new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(k => k.InstitutionId),
            new CreateIndexOptions { Name = "ix_apikeys_institution" }));

        await WalletLedgerEntries.Indexes.CreateOneAsync(new CreateIndexModel<WalletLedgerEntry>(
            Builders<WalletLedgerEntry>.IndexKeys
                .Ascending(w => w.InstitutionId)
                .Descending(w => w.CreatedAt),
            new CreateIndexOptions { Name = "ix_wallet_inst_created" }));

        await VerificationCalls.Indexes.CreateOneAsync(new CreateIndexModel<VerificationCall>(
            Builders<VerificationCall>.IndexKeys
                .Ascending(v => v.InstitutionId)
                .Descending(v => v.CreatedAt),
            new CreateIndexOptions { Name = "ix_calls_inst_created" }));

        try { await VerificationCalls.Indexes.DropOneAsync("ix_calls_idempotency"); } catch { /* index may not exist */ }
        await VerificationCalls.Indexes.CreateOneAsync(new CreateIndexModel<VerificationCall>(
            Builders<VerificationCall>.IndexKeys.Ascending(v => v.IdempotencyKey),
            new CreateIndexOptions { Sparse = true, Unique = false, Name = "ix_calls_idempotency" }));

        await PricingRates.Indexes.CreateOneAsync(new CreateIndexModel<PricingRate>(
            Builders<PricingRate>.IndexKeys
                .Ascending(r => r.Type)
                .Ascending(r => r.InstitutionId),
            new CreateIndexOptions { Name = "ix_pricing_type_inst" }));

        await AuditLogs.Indexes.CreateOneAsync(new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(a => a.ActorId),
            new CreateIndexOptions { Name = "ix_audit_actor" }));

        await RefreshTokens.Indexes.CreateOneAsync(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.Token),
            new CreateIndexOptions { Unique = true, Name = "ix_refresh_token" }));

        await RefreshTokens.Indexes.CreateOneAsync(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.UserId),
            new CreateIndexOptions { Name = "ix_refresh_user" }));

        await NimcConfigs.Indexes.CreateOneAsync(new CreateIndexModel<NimcConfig>(
            Builders<NimcConfig>.IndexKeys.Ascending(c => c.ActiveEnvironment),
            new CreateIndexOptions { Unique = true, Name = "ix_nimc_env" }));
    }
}
