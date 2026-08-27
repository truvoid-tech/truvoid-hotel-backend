using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TruvoID.Components.Services;

/// <summary>
/// Custom AuthenticationStateProvider that reads the JWT access token from localStorage
/// and creates a ClaimsPrincipal from it.
/// </summary>
public class TruvoIDAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenService _tokenService;

    public TruvoIDAuthStateProvider(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenService.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var expiry = await _tokenService.GetTokenExpiryAsync();
        if (expiry.HasValue && expiry.Value <= DateTime.UtcNow)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationState(principal);
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string token)
    {
        var claims = new List<Claim>();

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return claims;

            var payload = parts[1];
            // Add padding if needed
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("nameid", out var nameId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId.GetString() ?? ""));
            if (doc.RootElement.TryGetProperty("email", out var email))
                claims.Add(new Claim(ClaimTypes.Email, email.GetString() ?? ""));
            if (doc.RootElement.TryGetProperty("institution_id", out var instId))
                claims.Add(new Claim("institution_id", instId.GetString() ?? ""));
            if (doc.RootElement.TryGetProperty("role", out var role))
                claims.Add(new Claim(ClaimTypes.Role, role.GetString() ?? ""));
            if (doc.RootElement.TryGetProperty("institution_name", out var instName))
                claims.Add(new Claim("institution_name", instName.GetString() ?? ""));
        }
        catch
        {
            // If parsing fails, return empty claims
        }

        return claims;
    }
}
