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
    private const string IdaccessBaseUrl = "https://idaccess.info/v1";

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
            var apiKey = ResolveIdaccessApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException($"IDACCESS_API_KEY not configured. Checked: IDACCESS_API_KEY, IDACCESS-API-KEY in both IConfiguration and Environment.");
            }

            var client = _httpClientFactory.CreateClient("idaccess");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            // Per IDaccess docs: Idempotency-Key header required
            var upstreamIdempotencyKey = idempotencyKey ?? $"req_{call.Id:N}";
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", upstreamIdempotencyKey);

            // Determine endpoint and body field per IDaccess docs
            string endpoint = type switch
            {
                VerificationType.Nin => "identity/nin",
                VerificationType.Bvn => "identity/bvn",
                VerificationType.Phone => "identity/phone",
                _ => throw new NotSupportedException($"Verification type {type} is not supported.")
            };
            string bodyField = type switch
            {
                VerificationType.Nin => "nin",
                VerificationType.Bvn => "bvn",
                VerificationType.Phone => "phone",
                _ => throw new NotSupportedException($"Verification type {type} is not supported.")
            };

            var requestBody = new Dictionary<string, string> { { bodyField, subjectRef.Trim() } };
            Console.WriteLine($"[VERIFY] Calling {IdaccessBaseUrl}/{endpoint}");
            var httpResponse = await client.PostAsJsonAsync($"{IdaccessBaseUrl}/{endpoint}", requestBody, ct);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"[VERIFY] HTTP {(int)httpResponse.StatusCode}: {responseContent}");

            if (httpResponse.IsSuccessStatusCode)
            {
                var resultDoc = JsonDocument.Parse(responseContent);
                var resultRoot = resultDoc.RootElement;
                var apiSuccess = resultRoot.TryGetProperty("success", out var successProp) && successProp.ValueKind == JsonValueKind.True;

                if (apiSuccess && resultRoot.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
                {
                    var matchedData = new
                    {
                        name = dataProp.TryGetProperty("name", out var nProp) ? nProp.GetString() : null,
                        dob = dataProp.TryGetProperty("dob", out var dProp) ? dProp.GetString() : null,
                        phone = dataProp.TryGetProperty("phone", out var pProp) ? pProp.GetString() : null,
                        gender = dataProp.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null,
                        photo = dataProp.TryGetProperty("photo", out var phProp) ? phProp.GetString() : null
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
                    var errorMessage = "Verification returned no match.";
                    if (resultRoot.TryGetProperty("error", out var errObj) && errObj.ValueKind == JsonValueKind.Object)
                        if (errObj.TryGetProperty("message", out var mp))
                            errorMessage = mp.GetString() ?? errorMessage;

                    var isNoMatch = errorMessage.Contains("no match", StringComparison.OrdinalIgnoreCase);
                    var callStatus = isNoMatch ? VerificationStatus.NoMatch : VerificationStatus.Error;
                    var errUpdate = Builders<VerificationCall>.Update
                        .Set(c => c.Status, callStatus)
                        .Set(c => c.ErrorMessage, errorMessage)
                        .Set(c => c.RawResponseJson, responseContent)
                        .Set(c => c.UpdatedAt, DateTime.UtcNow);
                    await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, errUpdate, cancellationToken: ct);
                    if (!isNoMatch)
                        await _walletService.CreditAsync(institutionId, price, $"Refund: {type} upstream error", call.Id.ToString(), ct);
                }
            }
            else
            {
                // Upstream returned an error
                var errorMessage = $"Upstream returned HTTP {(int)httpResponse.StatusCode}.";
                try
                {
                    var errDoc = JsonDocument.Parse(responseContent);
                    if (errDoc.RootElement.TryGetProperty("message", out var msgProp))
                        errorMessage = msgProp.GetString() ?? errorMessage;
                }
                catch { }

                var status = VerificationStatus.Error;

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

    
    private string? ResolveIdaccessApiKey()
    {
        var key = _configuration["IDACCESS_API_KEY"]
            ?? _configuration["IDACCESS-API-KEY"]
            ?? Environment.GetEnvironmentVariable("IDACCESS_API_KEY")
            ?? Environment.GetEnvironmentVariable("IDACCESS-API-KEY");
        if (!string.IsNullOrEmpty(key)) return key;

        foreach (var envKey in Environment.GetEnvironmentVariables().Keys)
        {
            var rawKey = envKey.ToString()!;
            var k = rawKey.Trim();
            if (k.StartsWith("IDACCESS_API_KEY", StringComparison.OrdinalIgnoreCase))
            {
                var v = Environment.GetEnvironmentVariable(rawKey);
                Console.WriteLine($"[VERIFY] Found key via fuzzy: {k}");
                return v;
            }
        }
        return null;
    }

    private static string HashSubjectRef(string subjectRef)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(subjectRef));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
