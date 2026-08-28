using MongoDB.Driver;
using TruvoID.Domain.Entities;

namespace TruvoID.Infrastructure.Data;

/// <summary>
/// MongoDB context replacing TruvoIDDbContext. Provides typed collection accessors.
/// No migrations needed — collections are created automatically on first insert.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

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

    /// <summary>
    /// Ensure indexes exist for performance. Called once on startup.
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        // Users — unique email
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true, Name = "ix_users_email" }));

        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.InstitutionId),
            new CreateIndexOptions { Name = "ix_users_institution" }));

        // Institutions — unique name
        await Institutions.Indexes.CreateOneAsync(new CreateIndexModel<Institution>(
            Builders<Institution>.IndexKeys.Ascending(i => i.Name),
            new CreateIndexOptions { Unique = true, Name = "ix_institutions_name" }));

        // API Keys — unique hash, lookup by institution
        await ApiKeys.Indexes.CreateOneAsync(new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(k => k.KeyHash),
            new CreateIndexOptions { Unique = true, Name = "ix_apikeys_hash" }));

        await ApiKeys.Indexes.CreateOneAsync(new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(k => k.InstitutionId),
            new CreateIndexOptions { Name = "ix_apikeys_institution" }));

        // Wallet Ledger — lookup by institution + created_at
        await WalletLedgerEntries.Indexes.CreateOneAsync(new CreateIndexModel<WalletLedgerEntry>(
            Builders<WalletLedgerEntry>.IndexKeys
                .Ascending(w => w.InstitutionId)
                .Descending(w => w.CreatedAt),
            new CreateIndexOptions { Name = "ix_wallet_inst_created" }));

        // Verification Calls — lookup by institution, idempotency key, created_at
        await VerificationCalls.Indexes.CreateOneAsync(new CreateIndexModel<VerificationCall>(
            Builders<VerificationCall>.IndexKeys
                .Ascending(v => v.InstitutionId)
                .Descending(v => v.CreatedAt),
            new CreateIndexOptions { Name = "ix_calls_inst_created" }));

        await VerificationCalls.Indexes.CreateOneAsync(new CreateIndexModel<VerificationCall>(
            Builders<VerificationCall>.IndexKeys.Ascending(v => v.IdempotencyKey),
            new CreateIndexOptions { Sparse = true, Unique = true, Name = "ix_calls_idempotency" }));

        // Pricing Rates — lookup by type + institution
        await PricingRates.Indexes.CreateOneAsync(new CreateIndexModel<PricingRate>(
            Builders<PricingRate>.IndexKeys
                .Ascending(r => r.Type)
                .Ascending(r => r.InstitutionId),
            new CreateIndexOptions { Name = "ix_pricing_type_inst" }));

        // Audit Logs — lookup by actor, entity, created_at
        await AuditLogs.Indexes.CreateOneAsync(new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(a => a.ActorId),
            new CreateIndexOptions { Name = "ix_audit_actor" }));

        // Refresh Tokens — lookup by token, user
        await RefreshTokens.Indexes.CreateOneAsync(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.Token),
            new CreateIndexOptions { Unique = true, Name = "ix_refresh_token" }));

        await RefreshTokens.Indexes.CreateOneAsync(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.UserId),
            new CreateIndexOptions { Name = "ix_refresh_user" }));
    }
}
