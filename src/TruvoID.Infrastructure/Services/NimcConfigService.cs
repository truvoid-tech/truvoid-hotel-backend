using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class NimcConfigService : INimcConfigService
{
    private readonly MongoDbContext _db;

    public NimcConfigService(MongoDbContext db) => _db = db;

    public async Task<List<NimcConfigDto>> GetAllAsync(CancellationToken ct = default)
    {
        var configs = await _db.NimcConfigs.Find(_ => true).ToListAsync(ct);
        return configs.Select(MapToDto).ToList();
    }

    public async Task<NimcConfigDto?> GetActiveAsync(CancellationToken ct = default)
    {
        var config = await _db.NimcConfigs.Find(c => c.IsActive).FirstOrDefaultAsync(ct);
        return config is null ? null : MapToDto(config);
    }

    public async Task UpsertAsync(string environment, UpdateNimcConfigRequest request, CancellationToken ct = default)
    {
        var existing = await _db.NimcConfigs.Find(c => c.Environment == environment).FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            var update = Builders<NimcConfig>.Update
                .Set(c => c.ApiBaseUrl, request.ApiBaseUrl)
                .Set(c => c.ApiKey, request.ApiKey)
                .Set(c => c.PartnerId, request.PartnerId)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            if (request.SecretKey is not null)
                update = update.Set(c => c.SecretKey, request.SecretKey);

            await _db.NimcConfigs.UpdateOneAsync(c => c.Id == existing.Id, update, cancellationToken: ct);
        }
        else
        {
            var config = new NimcConfig
            {
                Environment = environment,
                ApiBaseUrl = request.ApiBaseUrl,
                ApiKey = request.ApiKey,
                PartnerId = request.PartnerId,
                SecretKey = request.SecretKey,
                IsActive = false
            };
            await _db.NimcConfigs.InsertOneAsync(config, cancellationToken: ct);
        }
    }

    public async Task ActivateAsync(string environment, CancellationToken ct = default)
    {
        // Deactivate all
        var deactivate = Builders<NimcConfig>.Update.Set(c => c.IsActive, false);
        await _db.NimcConfigs.UpdateManyAsync(_ => true, deactivate, cancellationToken: ct);

        // Activate target
        var activate = Builders<NimcConfig>.Update
            .Set(c => c.IsActive, true)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _db.NimcConfigs.UpdateOneAsync(c => c.Environment == environment, activate, cancellationToken: ct);
    }

    private static NimcConfigDto MapToDto(NimcConfig c) => new()
    {
        Environment = c.Environment,
        ApiBaseUrl = c.ApiBaseUrl,
        ApiKey = c.ApiKey.Length > 8 ? c.ApiKey[..4] + "••••" + c.ApiKey[^4..] : "••••",
        PartnerId = c.PartnerId,
        IsActive = c.IsActive
    };
}
