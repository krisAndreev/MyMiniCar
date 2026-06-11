using System.Net.Http.Json;
using MyMiniCar.Web.Models;

namespace MyMiniCar.Web.Services;

/// <summary>IProductService backed by the MyMiniCar Api (Supabase-backed).
/// Replaces MockProductService.</summary>
public class ApiProductService : IProductService
{
    private readonly HttpClient _http;

    public ApiProductService(string apiBaseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    }

    public async Task<IEnumerable<Product>> GetProductsAsync()
        => await _http.GetFromJsonAsync<List<Product>>("/api/products") ?? new();

    public async Task<IEnumerable<Product>> GetFeaturedProductsAsync()
        => await _http.GetFromJsonAsync<List<Product>>("/api/products/featured") ?? new();

    public async Task<Product?> GetProductByIdAsync(string id)
    {
        var resp = await _http.GetAsync($"/api/products/{id}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<Product>();
    }
}
