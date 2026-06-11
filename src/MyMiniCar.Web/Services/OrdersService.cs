using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

/// <summary>Reads the signed-in user's orders from the Api (bearer-authed).</summary>
public sealed class OrdersService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public OrdersService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<List<OrderView>?> GetMineAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/orders/mine");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<List<OrderView>>();
    }
}

public sealed record OrderItemView(string Name, decimal UnitPrice, int Quantity);
public sealed record OrderView(
    Guid Id, string Status, decimal Total, string Currency,
    DateTime CreatedAt, List<OrderItemView> Items);
