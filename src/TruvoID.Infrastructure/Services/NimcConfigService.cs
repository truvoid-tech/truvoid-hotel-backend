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

    public async Task<NimcEnvironmentDto> GetActiveEnvironmentAsync(CancellationToken ct = default)
    {
        var config = await _db.NimcConfigs.Find(_ => true).FirstOrDefaultAsync(ct);
        return new NimcEnvironmentDto { ActiveEnvironment = config?.ActiveEnvironment ?? "sandbox" };
    }

    public async Task SetActiveEnvironmentAsync(string environment, CancellationToken ct = default)
    {
        var config = await _db.NimcConfigs.Find(_ => true).FirstOrDefaultAsync(ct);

        if (config is not null)
        {
            var update = Builders<NimcConfig>.Update
                .Set(c => c.ActiveEnvironment, environment)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);
            await _db.NimcConfigs.UpdateOneAsync(c => c.Id == config.Id, update, cancellationToken: ct);
        }
        else
        {
            await _db.NimcConfigs.InsertOneAsync(new NimcConfig
            {
                ActiveEnvironment = environment
            }, cancellationToken: ct);
        }
    }
}
