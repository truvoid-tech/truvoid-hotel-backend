using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

/// <summary>
/// Verification service — orchestrates the full verification call flow.
/// For now, this is a stub that returns mock results.
/// The real implementation will call the NIMC partner API.
/// </summary>
public class VerificationService : IVerificationService
{
    private readonly TruvoIDDbContext _db;
    private readonly IWalletService _walletService;
    private readonly IPricingService _pricingService;
    private readonly IAuditService _auditService;

    public VerificationService(
        TruvoIDDbContext db,
        IWalletService walletService,
        IPricingService pricingService,
        IAuditService auditService)
    {
        _db = db;
        _walletService = walletService;
        _pricingService = pricingService;
        _auditService = auditService;
    }

    public async Task<VerificationResponse> VerifyAsync(
        Guid institutionId,
        VerificationType type,
        string subjectRef,
        Guid? userId = null,
        Guid? apiKeyId = null,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        // 1. Check idempotency
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _db.VerificationCalls
                .FirstOrDefaultAsync(c => c.IdempotencyKey == idempotencyKey && c.InstitutionId == institutionId, ct);

            if (existing is not null)
            {
                return MapToResponse(existing);
            }
        }

        // 2. Get price for this verification type
        decimal price;
        try
        {
            price = await _pricingService.GetPriceAsync(type, institutionId, ct);
        }
        catch (InvalidOperationException)
        {
            return new VerificationResponse
            {
                Status = "error",
                ErrorCode = ErrorCodes.InternalError,
                ErrorMessage = "Pricing not configured for this verification type."
            };
        }

        // 3. Check wallet balance
        var hasBalance = await _walletService.HasSufficientBalanceAsync(institutionId, price, ct);
        if (!hasBalance)
        {
            return new VerificationResponse
            {
                Status = "error",
                ErrorCode = ErrorCodes.InsufficientBalance,
                ErrorMessage = $"Insufficient wallet balance. Required: ₦{price:N2}"
            };
        }

        // 4. Create call record (pending)
        var call = new TruvoID.Domain.Entities.VerificationCall
        {
            InstitutionId = institutionId,
            UserId = userId,
            ApiKeyId = apiKeyId,
            Type = type,
            SubjectRef = HashSubjectRef(subjectRef),
            Status = VerificationStatus.Pending,
            AmountCharged = price,
            IdempotencyKey = idempotencyKey
        };

        _db.VerificationCalls.Add(call);
        await _db.SaveChangesAsync(ct);

        // 5. Debit wallet
        var debitResult = await _walletService.DebitAsync(institutionId, price, $"Verification: {type}", call.Id.ToString(), ct);
        if (!debitResult.Success)
        {
            call.Status = VerificationStatus.Error;
            call.ErrorMessage = debitResult.ErrorMessage;
            await _db.SaveChangesAsync(ct);

            return new VerificationResponse
            {
                Status = "error",
                ErrorCode = ErrorCodes.InsufficientBalance,
                ErrorMessage = debitResult.ErrorMessage
            };
        }

        // 6. Link debit to call
        call.LedgerEntryId = debitResult.LedgerEntryId;
        await _db.SaveChangesAsync(ct);

        // 7. Call NIMC partner API (stubbed for now)
        // TODO: Replace with real NIMC partner API call
        call.Status = VerificationStatus.Match;
        call.MatchedFieldsJson = JsonSerializer.Serialize(new { name = "Sample Match", dob = "1990-01-01", phone = "08012345678", gender = "Male" });
        call.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 8. Audit log
        await _auditService.LogAsync(
            AuditAction.Verified,
            nameof(TruvoID.Domain.Entities.VerificationCall),
            call.Id,
            userId ?? apiKeyId,
            userId.HasValue ? "User" : "ApiKey",
            ipAddress: null,
            ct: ct);

        return MapToResponse(call);
    }

    private static VerificationResponse MapToResponse(TruvoID.Domain.Entities.VerificationCall call)
    {
        VerificationData? data = null;
        if (!string.IsNullOrEmpty(call.MatchedFieldsJson))
        {
            try
            {
                var doc = JsonDocument.Parse(call.MatchedFieldsJson);
                var root = doc.RootElement;
                data = new VerificationData
                {
                    Name = root.TryGetProperty("name", out var nProp) ? nProp.GetString() : null,
                    DateOfBirth = root.TryGetProperty("dob", out var dProp) ? dProp.GetString() : null,
                    PhoneNumber = root.TryGetProperty("phone", out var pProp) ? pProp.GetString() : null,
                    Gender = root.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null
                };
            }
            catch
            {
                // If JSON parsing fails, return null data
            }
        }

        return new VerificationResponse
        {
            Status = call.Status switch
            {
                VerificationStatus.Match => "match",
                VerificationStatus.NoMatch => "no_match",
                _ => "error"
            },
            Data = data,
            CallId = call.Id.ToString(),
            WalletBalanceAfter = call.LedgerEntry?.BalanceAfter ?? 0,
            ErrorCode = call.Status == VerificationStatus.Error ? ErrorCodes.UpstreamError : null,
            ErrorMessage = call.ErrorMessage
        };
    }

    private static string HashSubjectRef(string subjectRef)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(subjectRef));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
