using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class VerificationCall
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public VerificationType Type { get; set; }
    public string SubjectRef { get; set; } = string.Empty; // SHA-256 hashed
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public decimal AmountCharged { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? LedgerEntryId { get; set; }
    public string? MatchedFieldsJson { get; set; }
    public string? RawResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
