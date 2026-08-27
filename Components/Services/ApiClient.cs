using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace TruvoID.Components.Services;

/// <summary>
/// HTTP client wrapper that automatically attaches the JWT access token
/// and handles common API patterns (JSON errors, 401 redirects).
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly TokenService _tokenService;
    private readonly NavigationManager _nav;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(HttpClient http, TokenService tokenService, NavigationManager nav)
    {
        _http = http;
        _tokenService = tokenService;
        _nav = nav;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, object? body = null)
    {
        var token = await _tokenService.GetAccessTokenAsync();
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, url);
        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _tokenService.ClearTokensAsync();
            _nav.NavigateTo("/login", forceLoad: true);
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public async Task<T?> PostAsync<T>(string url, object body)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, url, body);
        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _tokenService.ClearTokensAsync();
            _nav.NavigateTo("/login", forceLoad: true);
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public async Task<HttpResponseMessage> PostRawAsync(string url, object body)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, url, body);
        return await _http.SendAsync(request);
    }

    public async Task<T?> PutAsync<T>(string url, object body)
    {
        var request = await CreateRequestAsync(HttpMethod.Put, url, body);
        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _tokenService.ClearTokensAsync();
            _nav.NavigateTo("/login", forceLoad: true);
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public async Task<HttpResponseMessage> PutRawAsync(string url, object body)
    {
        var request = await CreateRequestAsync(HttpMethod.Put, url, body);
        return await _http.SendAsync(request);
    }

    public async Task<T?> DeleteAsync<T>(string url)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, url);
        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _tokenService.ClearTokensAsync();
            _nav.NavigateTo("/login", forceLoad: true);
            return default;
        }

        response.EnsureSuccessStatusCode();

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return default;

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public async Task<HttpResponseMessage> DeleteRawAsync(string url)
    {
        var request = await CreateRequestAsync(HttpMethod.Delete, url);
        return await _http.SendAsync(request);
    }

    /// <summary>
    /// Parse error message from API error response.
    /// </summary>
    public static async Task<string> GetErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? "An error occurred";
        }
        catch { }
        return $"Request failed with status {response.StatusCode}";
    }
}
