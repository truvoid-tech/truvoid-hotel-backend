using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TruvoID.Domain.Entities;

public class NimcConfig
{
    [BsonId]
    public Guid Id { get; set; }

    public string Environment { get; set; } = "sandbox"; // sandbox, live
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
