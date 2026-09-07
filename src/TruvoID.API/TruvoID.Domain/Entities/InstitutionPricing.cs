using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TruvoID.Domain.Entities;

/// <summary>
/// Per-institution verification pricing. Each institution can have custom rates.
/// If no record exists for an institution, global defaults apply.
/// </summary>
public class InstitutionPricing
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public Guid InstitutionId { get; set; }

    public decimal NinPrice { get; set; } = 100m;
    public decimal BvnPrice { get; set; } = 150m;
    public decimal PhonePrice { get; set; } = 50m;

    /// <summary>TruvoID's internal cost per NIN verification (paid to NIMC).</summary>
    public decimal NinCost { get; set; } = 45m;

    /// <summary>TruvoID's internal cost per BVN verification.</summary>
    public decimal BvnCost { get; set; } = 65m;

    /// <summary>TruvoID's internal cost per Phone verification.</summary>
    public decimal PhoneCost { get; set; } = 20m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
