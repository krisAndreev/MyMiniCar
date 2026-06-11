using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

public sealed class AdminAnalyticsService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public AdminAnalyticsService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<AnalyticsSummary?> GetAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return null;
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/analytics");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AnalyticsSummary>() : null;
    }
}

public sealed record StatusCount(string Status, int Count);
public sealed record TopProduct(string Name, int Quantity, decimal Revenue);
public sealed record RevenuePoint(DateTime Day, decimal Revenue);
public sealed record AnalyticsSummary(
    decimal TotalRevenue, int OrderCount, int PaidOrderCount, decimal AverageOrderValue,
    List<StatusCount> StatusCounts, List<TopProduct> TopProducts, List<RevenuePoint> RevenueByDay);
