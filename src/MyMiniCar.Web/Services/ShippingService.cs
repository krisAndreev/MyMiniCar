using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

public class ShippingService
{
    private readonly HttpClient _http;
    private Task<List<EcontCity>>? _citiesTask;

    public ShippingService(string apiBaseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    }

    public async Task<ShippingQuote?> GetQuoteAsync(ShippingQuoteRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/shipping/quote", request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ShippingQuote>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task<List<EcontCity>> GetCitiesAsync() => _citiesTask ??= LoadCitiesAsync();

    private async Task<List<EcontCity>> LoadCitiesAsync()
    {
        try
        {
            var cities = await _http.GetFromJsonAsync<List<EcontCity>>("/api/shipping/cities");
            if (cities is { Count: > 0 }) return cities;
        }
        catch (HttpRequestException) { }

        _citiesTask = null;
        return new List<EcontCity>();
    }

    public async Task<List<EcontOffice>> GetOfficesAsync(int cityId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<EcontOffice>>($"/api/shipping/offices?cityId={cityId}")
                ?? new List<EcontOffice>();
        }
        catch (HttpRequestException)
        {
            return new List<EcontOffice>();
        }
    }

    public async Task<LabelResult?> CreateLabelAsync(string sessionId)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/shipping/label", new { sessionId });
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<LabelResult>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

public record ShippingQuoteRequest(
    string DeliveryMode,
    string? OfficeCode,
    string? CityName,
    string? PostCode,
    string? Street,
    string? Num,
    double WeightKg);

public record ShippingQuote(decimal Price, string Currency);

public record EcontCity(int Id, string Name, string? Region, string PostCode, bool HasOffice, bool HasDoorDelivery);

public record EcontOffice(string Code, string Name, string Address, string City, string PostCode);

public record LabelResult(string ShipmentNumber, string? PdfUrl, decimal Price, string Currency);
