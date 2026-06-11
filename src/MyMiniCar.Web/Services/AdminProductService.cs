using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

public sealed class AdminProductService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public AdminProductService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<List<AdminProduct>?> GetAllAsync()
    {
        var req = await AuthedAsync(HttpMethod.Get, "/api/admin/products");
        if (req is null) return null;
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<List<AdminProduct>>() : null;
    }

    public async Task<bool> SaveAsync(AdminProduct p)
    {
        var req = await AuthedAsync(HttpMethod.Post, "/api/admin/products");
        if (req is null) return false;
        req.Content = JsonContent.Create(p);
        return (await _http.SendAsync(req)).IsSuccessStatusCode;
    }

    public async Task<bool> SetActiveAsync(string id, bool active)
    {
        var req = await AuthedAsync(HttpMethod.Post, $"/api/admin/products/{id}/active?active={active.ToString().ToLower()}");
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

public sealed class AdminProduct
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? NameBg { get; set; }
    public string? DescriptionBg { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? DisplayModelUrl { get; set; }
    public string? PrintModelUrl { get; set; }
    public string? DefaultMaterial { get; set; }
    public string? Dimensions { get; set; }
    public int WeightGrams { get; set; } = 250;
    public string TileClass { get; set; } = "fil-blue";
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
