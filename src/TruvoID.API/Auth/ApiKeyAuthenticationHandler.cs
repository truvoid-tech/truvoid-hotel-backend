using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TruvoID.Domain.Entities;
using TruvoID.Infrastructure.Data;

namespace TruvoID.API.Auth;

/// <summary>
/// Authenticates requests carrying an "X-API-Key" header against the api_keys
/// collection, as an alternative to a JWT bearer token. On success the resulting
/// principal carries the same "institution_id" claim shape the JWT flow uses, so
/// existing endpoint code (HttpContextExtensions.GetInstitutionId) works unchanged
/// regardless of which scheme actually authenticated the request.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-API-Key";

    private readonly MongoDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        MongoDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var rawKey = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawKey))
            return AuthenticateResult.NoResult();

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
        var key = await _db.ApiKeys.Find(k => k.KeyHash == hash).FirstOrDefaultAsync();

        if (key is null)
            return AuthenticateResult.Fail("Invalid API key.");
        if (key.Status != "Active")
            return AuthenticateResult.Fail("This API key has been revoked.");

        // Fire-and-forget usage tracking — don't block the request on it.
        _ = _db.ApiKeys.UpdateOneAsync(
            k => k.Id == key.Id,
            Builders<ApiKey>.Update
                .Inc(k => k.CallCount, 1)
                .Set(k => k.LastUsedAt, DateTime.UtcNow));

        var claims = new[]
        {
            new Claim("institution_id", key.InstitutionId.ToString()),
            new Claim("api_key_id", key.Id.ToString()),
            new Claim(ClaimTypes.Role, "ApiKey")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
