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
    private readonly INotificationService _notifications;
    private const string IdaccessBaseUrl = "https://idaccess.info/v1";

    public VerificationService(
        MongoDbContext db,
        IWalletService walletService,
        IPricingService pricingService,
        IAuditService auditService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        INotificationService notifications)
    {
        _db = db;
        _walletService = walletService;
        _pricingService = pricingService;
        _auditService = auditService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _notifications = notifications;
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
        try { price = await _pricingService.GetPriceAsync(type, institutionId, ct); }
        catch (InvalidOperationException)
        {
            return new VerificationResponse { Status = "error", ErrorCode = ErrorCodes.InternalError, ErrorMessage = "Pricing not configured for this verification type." };
        }

        // 3. Check wallet balance
        var hasBalance = await _walletService.HasSufficientBalanceAsync(institutionId, price, ct);
        if (!hasBalance)
        {
            return new VerificationResponse { Status = "error", ErrorCode = ErrorCodes.InsufficientBalance, ErrorMessage = $"Insufficient wallet balance. Required: \u20A6{price:N2}" };
        }

        // 4. Create call record
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
            await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id,
                Builders<VerificationCall>.Update.Set(c => c.Status, VerificationStatus.Error).Set(c => c.ErrorMessage, debitResult.ErrorMessage),
                cancellationToken: ct);
            return new VerificationResponse { Status = "error", ErrorCode = ErrorCodes.InsufficientBalance, ErrorMessage = debitResult.ErrorMessage };
        }

        // 6. Link debit
        await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id,
            Builders<VerificationCall>.Update.Set(c => c.LedgerEntryId, debitResult.LedgerEntryId), cancellationToken: ct);

        // 7. Call IDaccess API
        try
        {
            var apiKey = ResolveIdaccessApiKey();
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("IDACCESS_API_KEY not configured.");

            var client = _httpClientFactory.CreateClient("idaccess");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            var upstreamIdempotencyKey = idempotencyKey ?? $"req_{call.Id:N}";
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", upstreamIdempotencyKey);

            // Per IDaccess docs: POST /v1/identity/{type}
            var (endpoint, bodyField) = type switch
            {
                VerificationType.Nin => ("identity/nin/advance", "nin"),
                VerificationType.Bvn => ("identity/bvn/advance", "bvn"),
                VerificationType.Phone => ("identity/phone/basic", "phone_number"),
                _ => throw new NotSupportedException($"Verification type {type} is not supported.")
            };

            var requestBody = new Dictionary<string, string> { { bodyField, subjectRef.Trim() } };
            Console.WriteLine($"[VERIFY] Calling {IdaccessBaseUrl}/{endpoint} with {bodyField}={subjectRef.Trim()}");
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
                    // IDaccess returns nested data: { success, data: { verdict, data: { first_name, ... } } }
                    // Navigate into the inner data object for identity fields.
                    var identityData = dataProp;
                    if (dataProp.TryGetProperty("data", out var innerData) && innerData.ValueKind == JsonValueKind.Object)
                        identityData = innerData;

                    // Use the verdict field to distinguish match vs no-match
                    var verdict = dataProp.TryGetProperty("verdict", out var verdictProp) ? verdictProp.GetString() : null;
                    var isMatch = string.Equals(verdict, "MATCH", StringComparison.OrdinalIgnoreCase);

                    var firstName = identityData.TryGetProperty("first_name", out var fnProp) ? fnProp.GetString() : null;
                    var middleName = identityData.TryGetProperty("middle_name", out var mnProp) ? mnProp.GetString() : null;
                    var lastName = identityData.TryGetProperty("last_name", out var lnProp) ? lnProp.GetString() : null;
                    var fullName = string.Join(" ", new[] { firstName, middleName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    var rawPhoto = identityData.TryGetProperty("photograph", out var phProp) ? phProp.GetString() : null;
                    // IDaccess returns raw base64 JPEG — prefix it so the browser can render it inline.
                    var photoDataUrl = string.IsNullOrWhiteSpace(rawPhoto) ? null
                        : rawPhoto.StartsWith("data:") ? rawPhoto
                        : $"data:image/jpeg;base64,{rawPhoto}";
                    var matchedData = new
                    {
                        name = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                        dob = identityData.TryGetProperty("date_of_birth", out var dProp) ? dProp.GetString() : null,
                        phone = identityData.TryGetProperty("phone_number", out var pProp) ? pProp.GetString() : null,
                        gender = identityData.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null,
                        photo = photoDataUrl,
                        stateOfOrigin = identityData.TryGetProperty("state_of_origin", out var soProp) ? soProp.GetString() : null,
                        residentialAddress = identityData.TryGetProperty("residential_address", out var raProp) ? raProp.GetString() : null
                    };
                    var callStatus = isMatch ? VerificationStatus.Match : VerificationStatus.NoMatch;
                    var resultUpdate = Builders<VerificationCall>.Update
                        .Set(c => c.Status, callStatus)
                        .Set(c => c.MatchedFieldsJson, JsonSerializer.Serialize(matchedData))
                        .Set(c => c.RawResponseJson, responseContent)
                        .Set(c => c.UpdatedAt, DateTime.UtcNow);
                    await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id, resultUpdate, cancellationToken: ct);

                    // Fire-and-forget: don't block verification response on email delivery
                    _ = Task.Run(async () =>
                    {
                        await _notifications.CheckAndSendLowBalanceAlertAsync(call.InstitutionId, debitResult.BalanceAfter);
                        await _notifications.SendVerificationResultAsync(call.InstitutionId, type.ToString(), isMatch ? "Match" : "NoMatch", call.Id.ToString("N"), call.AmountCharged);
                    });
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

                    // Fire-and-forget: send low-balance alert regardless of match/no-match (wallet was still debited)
                    _ = Task.Run(async () =>
                    {
                        await _notifications.CheckAndSendLowBalanceAlertAsync(call.InstitutionId, debitResult.BalanceAfter);
                    });
                }
            }
            else
            {
                // HTTP error
                var errorMessage = $"Upstream returned HTTP {(int)httpResponse.StatusCode}.";
                try
                {
                    var errDoc = JsonDocument.Parse(responseContent);
                    var errRoot = errDoc.RootElement;
                    if (errRoot.TryGetProperty("error", out var errObj) && errObj.ValueKind == JsonValueKind.Object)
                    {
                        if (errObj.TryGetProperty("message", out var mp))
                            errorMessage = mp.GetString() ?? errorMessage;
                    }
                }
                catch { }
                await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id,
                    Builders<VerificationCall>.Update
                        .Set(c => c.Status, VerificationStatus.Error)
                        .Set(c => c.ErrorMessage, errorMessage)
                        .Set(c => c.RawResponseJson, responseContent)
                        .Set(c => c.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: ct);
                await _walletService.CreditAsync(institutionId, price, $"Refund: {type} upstream error", call.Id.ToString(), ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VERIFY] Exception: {ex.Message}");
            await _db.VerificationCalls.UpdateOneAsync(c => c.Id == call.Id,
                Builders<VerificationCall>.Update
                    .Set(c => c.Status, VerificationStatus.Error)
                    .Set(c => c.ErrorMessage, $"Upstream API error: {ex.Message}")
                    .Set(c => c.UpdatedAt, DateTime.UtcNow),
                cancellationToken: ct);
            await _walletService.CreditAsync(institutionId, price, $"Refund: {type} API error", call.Id.ToString(), ct);
        }

        // 8. Audit log
        await _auditService.LogAsync(AuditAction.Verified, nameof(VerificationCall), call.Id,
            userId ?? apiKeyId, userId.HasValue ? "User" : "ApiKey", ct: ct);

        var updatedCall = await _db.VerificationCalls.Find(c => c.Id == call.Id).FirstOrDefaultAsync(ct);
        var response = MapToResponse(updatedCall!);
        response.WalletBalanceAfter = debitResult.BalanceAfter;
        return response;
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
            var envKeyStr = envKey.ToString()!.Trim();
            if (envKeyStr.StartsWith("IDACCESS_API_KEY", StringComparison.OrdinalIgnoreCase))
            {
                var val = Environment.GetEnvironmentVariable(envKeyStr);
                Console.WriteLine($"[VERIFY] Found API key via fuzzy match: '{envKeyStr}'");
                return val;
            }
        }
        return null;
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
                    Name = root.TryGetProperty("name", out var np) ? np.GetString() : null,
                    DateOfBirth = root.TryGetProperty("dob", out var dp) ? dp.GetString() : null,
                    PhoneNumber = root.TryGetProperty("phone", out var pp) ? pp.GetString() : null,
                    Gender = root.TryGetProperty("gender", out var gp) ? gp.GetString() : null,
                    PhotoUrl = root.TryGetProperty("photo", out var pp2) ? pp2.GetString() : null,
                    StateOfOrigin = root.TryGetProperty("stateOfOrigin", out var soProp) ? soProp.GetString() : null,
                    ResidentialAddress = root.TryGetProperty("residentialAddress", out var raProp) ? raProp.GetString() : null
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
