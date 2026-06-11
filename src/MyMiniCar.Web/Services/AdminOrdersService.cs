using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

public sealed class AdminOrdersService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public AdminOrdersService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<List<AdminOrder>?> GetAllAsync()
    {
        var req = await AuthedAsync(HttpMethod.Get, "/api/admin/orders");
        if (req is null) return null;
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<List<AdminOrder>>() : null;
    }

    public async Task<bool> SetStatusAsync(Guid id, string status)
    {
        var req = await AuthedAsync(HttpMethod.Post, $"/api/admin/orders/{id}/status?status={status}");
        if (req is null) return false;
        return (await _http.SendAsync(req)).IsSuccessStatusCode;
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

public sealed record AdminOrderItem(string Name, decimal UnitPrice, int Quantity);
public sealed record AdminOrder(
    Guid Id, string Status, string? Email, string? CustomerName, decimal Total, string Currency,
    string? Carrier, string? ShippingMethod, DateTime CreatedAt, List<AdminOrderItem> Items);
