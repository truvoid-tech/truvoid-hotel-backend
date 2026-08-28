using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private const string IdaccessBaseUrl = "https://api.idaccess.info/v1";

    public VerificationService(
        MongoDbContext db,
        IWalletService walletService,
        IPricingService pricingService,
        IAuditService auditService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _db = db;
        _walletService = walletService;
        _pricingService = pricingService;
        _auditService = auditService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
                ErrorMessage = $"Insufficient wallet balance. Required: \u20A6{price:N2}"
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

        // 7. Call IDaccess partner API
        try
        {
            var apiKey = _configuration["IDACCESS_API_KEY"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("IDACCESS_API_KEY not configured.");
            }

            var client = _httpClientFactory.CreateClient("idaccess");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Determine endpoint based on verification type
            var endpoint = type switch
            {
                VerificationType.Nin => "nin/verify",
                VerificationType.Bvn => "bvn/verify",
                VerificationType.Phone => "phone/verify",
                _ => throw new NotSupportedException($"Verification type {type} is not supported.")
            };

            var requestBody = new { number = subjectRef };
            var httpResponse = await client.PostAsJsonAsync($"{IdaccessBaseUrl}/{endpoint}", requestBody, ct);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(ct);

            if (httpResponse.IsSuccessStatusCode)
            {
                var resultDoc = JsonDocument.Parse(responseContent);
                var resultRoot = resultDoc.RootElement;

                // Extract matched fields from IDaccess response
                string? firstName = resultRoot.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
                string? middleName = resultRoot.TryGetProperty("middle_name", out var mn) ? mn.GetString() : null;
                string? surname = resultRoot.TryGetProperty("last_name", out var ln) ? ln.GetString() : null;

                string? fullName = firstName;
                if (!string.IsNullOrEmpty(surname))
                    fullName = string.IsNullOrEmpty(firstName) ? surname : $"{firstName} {surname}";
                if (!string.IsNullOrEmpty(middleName) && fullName != null)
                    fullName = $"{fullName} {middleName}";

                var dob = resultRoot.TryGetProperty("date_of_birth", out var dobProp) ? dobProp.GetString() : null;
                var phone = resultRoot.TryGetProperty("phone_number", out var phProp) ? phProp.GetString() : null;
                var gender = resultRoot.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null;
                var photo = resultRoot.TryGetProperty("photo", out var photoProp) ? photoProp.GetString() : null;

                var matchedData = new
                {
                    name = fullName,
                    dob,
                    phone,
                    gender,
                    photo
                };

                var resultUpdate = Builders<VerificationCall>.Update
                    .Set(c => c.Status, VerificationStatus.Match)
                    .Set(c => c.MatchedFieldsJson, JsonSerializer.Serialize(matchedData))
                    .Set(c => c.RawResponseJson, responseContent)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow);
                await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, resultUpdate, cancellationToken: ct);
            }
            else
            {
                // Upstream returned an error
                var errorMessage = "Upstream verification failed.";
                try
                {
                    var errDoc = JsonDocument.Parse(responseContent);
                    if (errDoc.RootElement.TryGetProperty("message", out var msgProp))
                        errorMessage = msgProp.GetString() ?? errorMessage;
                }
                catch { }

                var status = responseContent.Contains("no match", StringComparison.OrdinalIgnoreCase)
                    ? VerificationStatus.NoMatch
                    : VerificationStatus.Error;

                var errorUpdate = Builders<VerificationCall>.Update
                    .Set(c => c.Status, status)
                    .Set(c => c.ErrorMessage, errorMessage)
                    .Set(c => c.RawResponseJson, responseContent)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow);
                await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, errorUpdate, cancellationToken: ct);

                // Reverse debit on upstream failure
                if (status == VerificationStatus.Error)
                {
                    await _walletService.CreditAsync(institutionId, price, $"Refund: {type} upstream error", call.Id.ToString(), ct);
                }
            }
        }
        catch (Exception ex)
        {
            // API call failed entirely
            var errorUpdate = Builders<VerificationCall>.Update
                .Set(c => c.Status, VerificationStatus.Error)
                .Set(c => c.ErrorMessage, $"Upstream API error: {ex.Message}")
                .Set(c => c.UpdatedAt, DateTime.UtcNow);
            await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, errorUpdate, cancellationToken: ct);

            // Reverse debit
            await _walletService.CreditAsync(institutionId, price, $"Refund: {type} API error", call.Id.ToString(), ct);
        }

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
                    Gender = root.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null,
                    PhotoUrl = root.TryGetProperty("photo", out var photoProp) ? photoProp.GetString() : null
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
