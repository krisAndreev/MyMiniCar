using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

/// <summary>
/// Talks to the MyMiniCar payments API, which holds the Stripe secret key and
/// creates Checkout Sessions. The browser only ever receives a redirect URL.
/// </summary>
public class CheckoutService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public CheckoutService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<CreateSessionResult?> CreateSessionAsync(CreateCheckoutRequest request)
    {
        var http = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/create-session")
        {
            Content = System.Net.Http.Json.JsonContent.Create(request)
        };
        var token = await _tokens.GetAsync();
        if (!string.IsNullOrWhiteSpace(token))
            http.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(http);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CreateSessionResult>();
    }

    public async Task<SessionStatus?> GetSessionAsync(string sessionId)
    {
        try
        {
            return await _http.GetFromJsonAsync<SessionStatus>($"/api/checkout/session/{sessionId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

public record CheckoutItem(string Name, string? Description, decimal UnitAmount, long Quantity);

public record CreateCheckoutRequest(
    List<CheckoutItem> Items,
    string? Email,
    string? Name,
    string? Phone,
    string? Address,
    string? City,
    string? PostalCode,
    string? Country,
    string DeliveryMode,
    string? OfficeCode,
    decimal ShippingAmount,
    double WeightKg,
    string ReturnBaseUrl);

public record CreateSessionResult(string Id, string Url);

public record SessionStatus(string? Status, string? PaymentStatus, string? Email, long? AmountTotal, string? Currency);
