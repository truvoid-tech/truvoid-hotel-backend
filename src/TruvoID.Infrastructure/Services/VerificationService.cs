using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using TruvoID.Core.DTOs;
using TruvoID.Core.Interfaces;
using TruvoID.Domain.Entities;
using TruvoID.Domain.Enums;
using TruvoID.Infrastructure.Data;

namespace TruvoID.Infrastructure.Services;

public class VerificationService : IVerificationService
{
    private readonly MongoDbContext _db;
    private readonly IWalletService _walletService;
    private readonly IPricingService _pricingService;
    private readonly IAuditService _auditService;

    public VerificationService(
        MongoDbContext db,
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
                .Find(c => c.IdempotencyKey == idempotencyKey && c.InstitutionId == institutionId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
                return MapToResponse(existing);
        }

        // 2. Get price
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
        var call = new VerificationCall
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
        await _db.VerificationCalls.InsertOneAsync(call, cancellationToken: ct);

        // 5. Debit wallet
        var debitResult = await _walletService.DebitAsync(institutionId, price, $"Verification: {type}", call.Id.ToString(), ct);
        if (!debitResult.Success)
        {
            var errorUpdate = Builders<VerificationCall>.Update
                .Set(c => c.Status, VerificationStatus.Error)
                .Set(c => c.ErrorMessage, debitResult.ErrorMessage);
            await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, errorUpdate, cancellationToken: ct);

            return new VerificationResponse
            {
                Status = "error",
                ErrorCode = ErrorCodes.InsufficientBalance,
                ErrorMessage = debitResult.ErrorMessage
            };
        }

        // 6. Link debit to call
        var linkUpdate = Builders<VerificationCall>.Update.Set(c => c.LedgerEntryId, debitResult.LedgerEntryId);
        await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, linkUpdate, cancellationToken: ct);

        // 7. Call NIMC partner API (stubbed for now)
        var resultUpdate = Builders<VerificationCall>.Update
            .Set(c => c.Status, VerificationStatus.Match)
            .Set(c => c.MatchedFieldsJson, JsonSerializer.Serialize(new { name = "Sample Match", dob = "1990-01-01", phone = "08012345678", gender = "Male" }))
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, resultUpdate, cancellationToken: ct);

        // 8. Audit log
        await _auditService.LogAsync(
            AuditAction.Verified,
            nameof(VerificationCall),
            call.Id,
            userId ?? apiKeyId,
            userId.HasValue ? "User" : "ApiKey",
            ct: ct);

        // Fetch updated call for response
        var updatedCall = await _db.VerificationCalls.Find(c => c.Id == call.Id).FirstOrDefaultAsync(ct);

        // Fetch balance after
        var balanceAfter = debitResult.BalanceAfter;
        var response = MapToResponse(updatedCall!);
        response.WalletBalanceAfter = balanceAfter;
        return response;
    }

    private static VerificationResponse MapToResponse(VerificationCall call)
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
            catch { }
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
            WalletBalanceAfter = 0,
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
