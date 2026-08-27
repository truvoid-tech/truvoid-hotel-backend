using Microsoft.JSInterop;

namespace TruvoID.Components.Services;

/// <summary>
/// Manages JWT access and refresh tokens via browser localStorage.
/// </summary>
public class TokenService
{
    private readonly IJSRuntime _js;
    private const string AccessTokenKey = "truvoid_access_token";
    private const string RefreshTokenKey = "truvoid_refresh_token";
    private const string ExpiryKey = "truvoid_token_expiry";

    public TokenService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        return await _js.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        return await _js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
    }

    public async Task<DateTime?> GetTokenExpiryAsync()
    {
        var expiryStr = await _js.InvokeAsync<string?>("localStorage.getItem", ExpiryKey);
        if (string.IsNullOrEmpty(expiryStr)) return null;
        return DateTime.TryParse(expiryStr, out var expiry) ? expiry : null;
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken, DateTime expiresAt)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, accessToken);
        await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
        await _js.InvokeVoidAsync("localStorage.setItem", ExpiryKey, expiresAt.ToString("O"));
    }

    public async Task ClearTokensAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ExpiryKey);
    }

    public async Task<bool> IsTokenExpiredAsync()
    {
        var expiry = await GetTokenExpiryAsync();
        return expiry is null || expiry <= DateTime.UtcNow;
    }
}
