using MyMiniCar.Api;
using Stripe;
using Stripe.Checkout;

const string CorsPolicy = "wasm";

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5229", "https://localhost:7109" };

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException(
        "Stripe:SecretKey is not configured. Run: dotnet user-secrets set \"Stripe:SecretKey\" \"sk_test_...\"");

app.UseCors(CorsPolicy);

const string Currency = "usd";
const decimal FreeShippingThreshold = 40m;
const decimal ShippingFee = 4.90m;

app.MapPost("/api/checkout/create-session", (CreateCheckoutRequest req) =>
{
    if (req.Items is null || req.Items.Count == 0)
        return Results.BadRequest(new { error = "Cart is empty." });

    if (string.IsNullOrWhiteSpace(req.ReturnBaseUrl))
        return Results.BadRequest(new { error = "Missing return URL." });

    var lineItems = req.Items.Select(i => new SessionLineItemOptions
    {
        Quantity = i.Quantity,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = Currency,
            UnitAmountDecimal = i.UnitAmount * 100m,
            ProductData = new SessionLineItemPriceDataProductDataOptions
            {
                Name = i.Name,
                Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description
            }
        }
    }).ToList();

    var subtotal = req.Items.Sum(i => i.UnitAmount * i.Quantity);
    if (subtotal < FreeShippingThreshold)
    {
        lineItems.Add(new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = Currency,
                UnitAmountDecimal = ShippingFee * 100m,
                ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Shipping" }
            }
        });
    }

    var baseUrl = req.ReturnBaseUrl.TrimEnd('/');
    var options = new SessionCreateOptions
    {
        Mode = "payment",
        LineItems = lineItems,
        CustomerEmail = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email,
        SuccessUrl = $"{baseUrl}/order-confirmed?session_id={{CHECKOUT_SESSION_ID}}",
        CancelUrl = $"{baseUrl}/checkout?canceled=true",
        Metadata = new Dictionary<string, string>
        {
            ["customer_name"] = req.Name ?? string.Empty,
            ["shipping_address"] = req.Address ?? string.Empty,
            ["shipping_city"] = req.City ?? string.Empty,
            ["shipping_postal"] = req.PostalCode ?? string.Empty,
            ["shipping_country"] = req.Country ?? string.Empty
        }
    };

    try
    {
        var session = new SessionService().Create(options);
        return Results.Ok(new { id = session.Id, url = session.Url });
    }
    catch (StripeException ex)
    {
        return Results.Problem($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
    }
});

// Looks up a session so the confirmation page can verify the payment really succeeded.
app.MapGet("/api/checkout/session/{id}", (string id) =>
{
    try
    {
        var session = new SessionService().Get(id);
        return Results.Ok(new
        {
            status = session.Status,
            paymentStatus = session.PaymentStatus,
            email = session.CustomerDetails?.Email ?? session.CustomerEmail,
            amountTotal = session.AmountTotal,
            currency = session.Currency
        });
    }
    catch (StripeException)
    {
        return Results.NotFound(new { error = "Session not found." });
    }
});

app.MapGet("/", () => "MyMiniCar payments API — POST /api/checkout/create-session");

app.Run();

namespace MyMiniCar.Api
{
    public record CreateCheckoutRequest(
        List<CheckoutItem> Items,
        string? Email,
        string? Name,
        string? Address,
        string? City,
        string? PostalCode,
        string? Country,
        string ReturnBaseUrl);

    public record CheckoutItem(string Name, string? Description, decimal UnitAmount, long Quantity);
}
