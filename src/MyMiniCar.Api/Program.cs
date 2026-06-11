using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using MyMiniCar.Api;
using MyMiniCar.Api.Data;
using Stripe;
using Stripe.Checkout;

const string CorsPolicy = "wasm";

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5229", "https://localhost:7109" };

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins)
          .SetIsOriginAllowed(origin =>
              builder.Environment.IsDevelopment()
              && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
              && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
          .AllowAnyHeader()
          .AllowAnyMethod()));

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

// TODO #REFACTOR - switch Econt demo creds/URL to the live contract before go-live
builder.Services.AddHttpClient<EcontService>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Econt:BaseUrl"] ?? "https://demo.econt.com/ee/services/";
    if (!baseUrl.EndsWith('/')) baseUrl += "/";
    http.BaseAddress = new Uri(baseUrl);

    var user = cfg["Econt:Username"] ?? "iasp-dev";
    var pass = cfg["Econt:Password"] ?? "1Asp-dev";
    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});

builder.Services.AddSingleton<SupabaseDataSource>();
builder.Services.AddScoped<ProductRepository>();

var app = builder.Build();
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"]
    ?? throw new InvalidOperationException(
        "Stripe:SecretKey is not configured. Run: dotnet user-secrets set \"Stripe:SecretKey\" \"sk_test_...\"");

app.UseResponseCompression();
app.UseCors(CorsPolicy);

_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EcontService>().GetCitiesAsync();
    }
    catch { }
});

const string Currency = "eur";

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

    // TODO #REFACTOR - re-price shipping server-side, don't trust the client amount
    if (req.ShippingAmount > 0m)
    {
        lineItems.Add(new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = Currency,
                UnitAmountDecimal = req.ShippingAmount * 100m,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = string.Equals(req.DeliveryMode, "office", StringComparison.OrdinalIgnoreCase)
                        ? "Доставка до офис на Еконт"
                        : "Доставка до адрес (Еконт)"
                }
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
            ["customer_phone"] = req.Phone ?? string.Empty,
            ["delivery_mode"] = req.DeliveryMode ?? "address",
            ["office_code"] = req.OfficeCode ?? string.Empty,
            ["shipping_address"] = req.Address ?? string.Empty,
            ["shipping_city"] = req.City ?? string.Empty,
            ["shipping_postal"] = req.PostalCode ?? string.Empty,
            ["shipping_country"] = req.Country ?? string.Empty,
            ["weight_kg"] = req.WeightKg.ToString(CultureInfo.InvariantCulture)
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

app.MapPost("/api/shipping/quote", async (ShippingQuoteRequest req, EcontService econt) =>
{
    try
    {
        var result = await econt.CalculateAsync(req);
        return result is null
            ? Results.Problem("Could not calculate a shipping price.")
            : Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Econt error: {ex.Message}");
    }
});

app.MapGet("/api/shipping/cities", async (EcontService econt) =>
{
    try
    {
        return Results.Ok(await econt.GetCitiesAsync());
    }
    catch (Exception ex)
    {
        return Results.Problem($"Econt error: {ex.Message}");
    }
});

app.MapGet("/api/shipping/offices", async (int cityId, EcontService econt) =>
{
    if (cityId <= 0)
        return Results.BadRequest(new { error = "A valid cityId is required." });

    try
    {
        return Results.Ok(await econt.GetOfficesAsync(cityId));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Econt error: {ex.Message}");
    }
});

// TODO #REFACTOR - book label via Stripe webhook, not on page load
// TODO #REFACTOR - make idempotent: one waybill per session
// TODO #REFACTOR - persist order + shipment number
app.MapPost("/api/shipping/label", async (CreateLabelRequest req, EcontService econt) =>
{
    Session session;
    try
    {
        session = new SessionService().Get(req.SessionId);
    }
    catch (StripeException)
    {
        return Results.NotFound(new { error = "Session not found." });
    }

    if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Payment not completed." });

    var m = session.Metadata ?? new Dictionary<string, string>();
    double.TryParse(m.GetValueOrDefault("weight_kg"), NumberStyles.Float, CultureInfo.InvariantCulture, out var weight);

    try
    {
        var result = await econt.CreateLabelAsync(
            deliveryMode: m.GetValueOrDefault("delivery_mode") ?? "address",
            officeCode: m.GetValueOrDefault("office_code"),
            cityName: m.GetValueOrDefault("shipping_city"),
            postCode: m.GetValueOrDefault("shipping_postal"),
            street: m.GetValueOrDefault("shipping_address"),
            num: null,
            weightKg: weight,
            receiverName: string.IsNullOrWhiteSpace(m.GetValueOrDefault("customer_name")) ? "Customer" : m["customer_name"],
            receiverPhone: string.IsNullOrWhiteSpace(m.GetValueOrDefault("customer_phone")) ? "0000000000" : m["customer_phone"],
            receiverEmail: session.CustomerDetails?.Email ?? session.CustomerEmail,
            description: "AURA order");

        return result is null
            ? Results.Problem("Could not create the Econt label.")
            : Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Econt error: {ex.Message}");
    }
});

app.MapGet("/api/products", async (ProductRepository repo) =>
    Results.Ok(await repo.GetActiveAsync()));

app.MapGet("/api/products/featured", async (ProductRepository repo) =>
    Results.Ok(await repo.GetFeaturedAsync()));

app.MapGet("/api/products/{id}", async (string id, ProductRepository repo) =>
{
    var product = await repo.GetByIdAsync(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

// DB connectivity probe. Returns 200 if the Supabase Postgres responds.
app.MapGet("/api/health/db", async (SupabaseDataSource db) =>
{
    try
    {
        return await db.CanConnectAsync()
            ? Results.Ok(new { db = "ok" })
            : Results.Problem("DB probe returned unexpected result.");
    }
    catch (Exception ex)
    {
        return Results.Problem($"DB connect failed: {ex.Message}");
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

    public record CheckoutItem(string Name, string? Description, decimal UnitAmount, long Quantity);
}
