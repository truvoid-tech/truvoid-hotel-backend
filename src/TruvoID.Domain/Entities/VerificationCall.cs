using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

public class VerificationCall : BaseEntity
{
    public Guid InstitutionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public VerificationType Type { get; set; }
    public string SubjectRef { get; set; } = string.Empty;
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public Guid? LedgerEntryId { get; set; }
    public decimal AmountCharged { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? MatchedFieldsJson { get; set; }
    public string? RawResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UpstreamReferenceId { get; set; }
}
