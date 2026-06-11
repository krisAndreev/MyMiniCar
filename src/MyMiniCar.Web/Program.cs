using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyMiniCar.Web;
using MyMiniCar.Web.Auth;
using MyMiniCar.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Custom Luxury Store Services
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5230";

builder.Services.AddScoped<IProductService>(_ => new ApiProductService(apiBaseUrl));
builder.Services.AddSingleton<CartService>();
builder.Services.AddSingleton<LanguageService>();

builder.Services.AddScoped(sp => new CheckoutService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped(sp => new OrdersService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped(sp => new DesignsService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped(sp => new AdminProductService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped(sp => new AdminOrdersService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddSingleton(_ => new ShippingService(apiBaseUrl));

// Auth (Supabase)
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "";
var supabaseAnon = builder.Configuration["Supabase:AnonKey"] ?? "";

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped(sp => new SupabaseAuthService(
    supabaseUrl, supabaseAnon, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped<SupabaseAuthStateProvider>(
    sp => new SupabaseAuthStateProvider(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<SupabaseAuthStateProvider>());
builder.Services.AddAuthorizationCore();

var host = builder.Build();

_ = host.Services.GetRequiredService<ShippingService>().GetCitiesAsync();

await host.RunAsync();
