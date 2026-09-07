using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TruvoID.Domain.Entities;

public class Institution
{
    [BsonId]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Active, Suspended
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
