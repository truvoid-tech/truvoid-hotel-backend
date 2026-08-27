using TruvoID.Domain.Enums;

namespace TruvoID.Domain.Entities;

/// <summary>
/// One row per verification call. Tracks who called, what type, result, and links to wallet debit.
/// subject_ref stores minimum data needed for dispute resolution — NOT the full NIMC response.
/// </summary>
public class VerificationCall : BaseEntity
{
    public Guid InstitutionId { get; set; }
    public Guid? UserId { get; set; } // Set if triggered from Dashboard
    public Guid? ApiKeyId { get; set; } // Set if triggered from API Gateway
    public VerificationType Type { get; set; }
    public string SubjectRef { get; set; } = string.Empty; // Hashed/minimal subject identifier
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public Guid? LedgerEntryId { get; set; } // Links to the debit transaction
    public decimal AmountCharged { get; set; }
    public string? IdempotencyKey { get; set; } // Client-supplied to prevent double-charging
    
    // Minimal result data (NOT the full NIMC payload)
    public string? MatchedFieldsJson { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UpstreamReferenceId { get; set; }
    
    // Navigation properties
    public Institution Institution { get; set; } = null!;
    public User? User { get; set; }
    public ApiKey? ApiKey { get; set; }
    public WalletLedgerEntry? LedgerEntry { get; set; }
}
