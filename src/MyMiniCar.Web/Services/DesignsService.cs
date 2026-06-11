using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

/// <summary>Saved Studio designs via the Api (bearer-authed).</summary>
public sealed class DesignsService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public DesignsService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<bool> SaveAsync(string? name, string configJson)
    {
        var req = await AuthedAsync(HttpMethod.Post, "/api/designs");
        if (req is null) return false;
        req.Content = JsonContent.Create(new { name, configJson });
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<DesignView>?> GetMineAsync()
    {
        var req = await AuthedAsync(HttpMethod.Get, "/api/designs/mine");
        if (req is null) return null;
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<List<DesignView>>();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var req = await AuthedAsync(HttpMethod.Delete, $"/api/designs/{id}");
        if (req is null) return false;
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    private async Task<HttpRequestMessage?> AuthedAsync(HttpMethod method, string path)
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return null;
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }
}

public sealed record DesignView(Guid Id, string? Name, string Config, DateTime CreatedAt);
