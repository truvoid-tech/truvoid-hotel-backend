using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class NimcConfigService : INimcConfigService
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _config;

    public NimcConfigService(MongoDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<NimcConfig?> GetByEnvironmentAsync(string environment)
    {
        return await _db.NimcConfigs
            .Find(c => c.Environment == environment)
            .FirstOrDefaultAsync();
    }

    public async Task<List<NimcConfig>> GetAllAsync()
    {
        return await _db.NimcConfigs.Find(_ => true).ToListAsync();
    }

    public async Task<NimcConfig?> GetActiveAsync()
    {
        return await _db.NimcConfigs
            .Find(c => c.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(string environment, string? apiKey)
    {
        var existing = await GetByEnvironmentAsync(environment);

        if (existing == null)
        {
            var newConfig = new NimcConfig
            {
                Id = Guid.NewGuid(),
                Environment = environment,
                ApiKey = apiKey,
                IsActive = false,
                UpdatedAt = DateTime.UtcNow
            };
            await _db.NimcConfigs.InsertOneAsync(newConfig);
        }
        else
        {
            var update = Builders<NimcConfig>.Update
                .Set(c => c.ApiKey, apiKey)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);
            await _db.NimcConfigs.UpdateOneAsync(
                c => c.Environment == environment, update);
        }
    }

    public async Task ActivateAsync(string environment)
    {
        // Deactivate all
        var deactivate = Builders<NimcConfig>.Update.Set(c => c.IsActive, false);
        await _db.NimcConfigs.UpdateManyAsync(_ => true, deactivate);

        // Activate selected
        var activate = Builders<NimcConfig>.Update
            .Set(c => c.IsActive, true)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _db.NimcConfigs.UpdateOneAsync(
            c => c.Environment == environment, activate);
    }

    public string GetApiKeyForEnvironment(string environment)
    {
        var prefix = environment.ToUpper() == "LIVE" ? "LIVE" : "SANDBOX";
        return _config[$"IDACCESS_API_KEY"] ?? string.Empty;
    }
}
