using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyMiniCar.Web;
using MyMiniCar.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Custom Luxury Store Services
builder.Services.AddScoped<IProductService, MockProductService>();
builder.Services.AddSingleton<CartService>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5230";
builder.Services.AddScoped(_ => new CheckoutService(apiBaseUrl));
builder.Services.AddScoped(_ => new ShippingService(apiBaseUrl));

await builder.Build().RunAsync();
