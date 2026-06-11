# Supabase Phase 1A — Products Read-Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.
>
> **Execution rule (token-safety):** ONE task per turn. Each task ends in a git commit + ticked boxes. Resume after interruption: open this file, find first unchecked box, continue. Working tree clean between tasks.

**Goal:** Serve the product catalog from the Supabase DB through `MyMiniCar.Api`, and make the Blazor `Web` read it via HTTP, replacing the hard-coded `MockProductService`.

**Architecture:** API-mediated (Approach A). `Web` (browser) → `Api` (`ProductRepository` over Npgsql) → Supabase `products`. Only active products are exposed; ordered by `sort_order`. `IProductService` interface stays unchanged so the swap is a one-line DI change.

**Tech Stack:** ASP.NET minimal API (.NET 7), Npgsql 7, Blazor WASM, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phase 0 complete — DB live, `SupabaseDataSource` registered, `/api/health/db` → ok.

---

## File Structure

- Create: `src/MyMiniCar.Api/Models/ProductDto.cs` — API response shape (mirrors `Web/Models/Product` field names)
- Create: `src/MyMiniCar.Api/Data/ProductRepository.cs` — Npgsql reads
- Modify: `src/MyMiniCar.Api/Program.cs` — register repo + 3 product endpoints
- Create: `src/MyMiniCar.Web/Services/ApiProductService.cs` — `IProductService` over HttpClient
- Modify: `src/MyMiniCar.Web/Program.cs` — swap `MockProductService` → `ApiProductService`

Note: `MockProductService` is kept in the repo (not deleted) as a fallback/reference; only the DI registration changes.

---

## Task 1: Api product DTO + repository

**Files:**
- Create: `src/MyMiniCar.Api/Models/ProductDto.cs`
- Create: `src/MyMiniCar.Api/Data/ProductRepository.cs`

- [x] **Step 1: Create the DTO**

`src/MyMiniCar.Api/Models/ProductDto.cs`:
```csharp
namespace MyMiniCar.Api.Models;

/// <summary>Product shape returned to the browser. Property names match
/// MyMiniCar.Web.Models.Product so the client deserializes directly.</summary>
public sealed record ProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    string Category,
    string DefaultMaterial,
    string Dimensions,
    bool IsFeatured,
    int WeightGrams,
    string TileClass);
```

- [x] **Step 2: Create the repository**

`src/MyMiniCar.Api/Data/ProductRepository.cs`:
```csharp
using MyMiniCar.Api.Models;
using Npgsql;

namespace MyMiniCar.Api.Data;

/// <summary>Read-only access to the products catalog.</summary>
public sealed class ProductRepository
{
    private readonly SupabaseDataSource _db;

    public ProductRepository(SupabaseDataSource db) => _db = db;

    private const string SelectColumns = @"
        id, name, description, price,
        coalesce(image_url, '')        as image_url,
        category,
        coalesce(default_material, '') as default_material,
        coalesce(dimensions, '')       as dimensions,
        is_featured, weight_grams,
        coalesce(tile_class, 'fil-blue') as tile_class";

    public Task<List<ProductDto>> GetActiveAsync(CancellationToken ct = default) =>
        QueryAsync($"select {SelectColumns} from public.products where is_active order by sort_order, name", null, ct);

    public Task<List<ProductDto>> GetFeaturedAsync(CancellationToken ct = default) =>
        QueryAsync($"select {SelectColumns} from public.products where is_active and is_featured order by sort_order, name", null, ct);

    public async Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var rows = await QueryAsync($"select {SelectColumns} from public.products where id = $1 and is_active", id, ct);
        return rows.FirstOrDefault();
    }

    private async Task<List<ProductDto>> QueryAsync(string sql, string? idParam, CancellationToken ct)
    {
        await using var cmd = _db.DataSource.CreateCommand(sql);
        if (idParam is not null) cmd.Parameters.AddWithValue(idParam);

        var list = new List<ProductDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProductDto(
                Id:              reader.GetString(0),
                Name:            reader.GetString(1),
                Description:     reader.GetString(2),
                Price:           reader.GetDecimal(3),
                ImageUrl:        reader.GetString(4),
                Category:        reader.GetString(5),
                DefaultMaterial: reader.GetString(6),
                Dimensions:      reader.GetString(7),
                IsFeatured:      reader.GetBoolean(8),
                WeightGrams:     reader.GetInt32(9),
                TileClass:       reader.GetString(10)));
        }
        return list;
    }
}
```

- [x] **Step 3: Build**

Run: `cd src/MyMiniCar.Api && dotnet build`
Expected: Build succeeded, 0 errors.

- [x] **Step 4: Commit**

```bash
git add src/MyMiniCar.Api/Models/ProductDto.cs src/MyMiniCar.Api/Data/ProductRepository.cs
git commit -m "feat(api): product DTO + Npgsql read repository

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Api product endpoints

**Files:**
- Modify: `src/MyMiniCar.Api/Program.cs`

- [ ] **Step 1: Register the repository**

In `src/MyMiniCar.Api/Program.cs`, next to `builder.Services.AddSingleton<SupabaseDataSource>();` add:
```csharp
builder.Services.AddScoped<ProductRepository>();
```

- [ ] **Step 2: Add the endpoints**

In `src/MyMiniCar.Api/Program.cs`, just before the `/api/health/db` endpoint, add:
```csharp
app.MapGet("/api/products", async (ProductRepository repo) =>
    Results.Ok(await repo.GetActiveAsync()));

app.MapGet("/api/products/featured", async (ProductRepository repo) =>
    Results.Ok(await repo.GetFeaturedAsync()));

app.MapGet("/api/products/{id}", async (string id, ProductRepository repo) =>
{
    var product = await repo.GetByIdAsync(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
});
```

- [ ] **Step 3: Build**

Run: `cd src/MyMiniCar.Api && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Smoke-test against live DB**

Run:
```bash
cd src/MyMiniCar.Api
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &)
sleep 8
curl -s http://localhost:5230/api/products | head -c 400; echo
curl -s http://localhost:5230/api/products/featured | python3 -c "import sys,json;print('featured=',len(json.load(sys.stdin)))"
curl -s http://localhost:5230/api/products/golf-keychain | head -c 200; echo
curl -s -o /dev/null -w "missing=%{http_code}\n" http://localhost:5230/api/products/does-not-exist
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: products JSON array (8 items), `featured= 3`, golf-keychain JSON, `missing=404`.

- [ ] **Step 5: Commit**

```bash
git add src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): GET /api/products endpoints (list, featured, by id)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Web ApiProductService + DI swap

**Files:**
- Create: `src/MyMiniCar.Web/Services/ApiProductService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`

- [ ] **Step 1: Create the service**

`src/MyMiniCar.Web/Services/ApiProductService.cs`:
```csharp
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
```

- [ ] **Step 2: Swap the DI registration**

In `src/MyMiniCar.Web/Program.cs`, replace:
```csharp
builder.Services.AddScoped<IProductService, MockProductService>();
```
with:
```csharp
builder.Services.AddScoped<IProductService>(_ => new ApiProductService(apiBaseUrl));
```
NOTE: `apiBaseUrl` is declared later in the file (`var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5230";`). Move that `apiBaseUrl` line UP so it sits **before** this registration. The result should read:
```csharp
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5230";

builder.Services.AddScoped<IProductService>(_ => new ApiProductService(apiBaseUrl));
builder.Services.AddSingleton<CartService>();
builder.Services.AddSingleton<LanguageService>();

builder.Services.AddScoped(_ => new CheckoutService(apiBaseUrl));
builder.Services.AddSingleton(_ => new ShippingService(apiBaseUrl));
```

- [ ] **Step 3: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MyMiniCar.Web/Services/ApiProductService.cs src/MyMiniCar.Web/Program.cs
git commit -m "feat(web): read products from Api, replace MockProductService

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: End-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Run Api + Web together**

Run:
```bash
cd /Users/kristiyanandreev/Desktop/MyMiniCar
(cd src/MyMiniCar.Api && dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &)
sleep 8
curl -s http://localhost:5230/api/products | python3 -c "import sys,json;d=json.load(sys.stdin);print('api products=',len(d))"
```
Expected: `api products= 8`.

- [ ] **Step 2: Confirm CORS allows the WASM origin**

Run:
```bash
curl -s -H "Origin: http://localhost:5229" -i http://localhost:5230/api/products | grep -i "access-control-allow-origin"
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: an `Access-Control-Allow-Origin` header echoing `http://localhost:5229` (CORS policy already configured in Program.cs for dev localhost).

- [ ] **Step 3: Mark plan complete + commit**

Tick all boxes, then:
```bash
git add docs/superpowers/plans/2026-06-11-supabase-phase1a-products.md
git commit -m "docs: Phase 1A complete"
```

---

## Self-Review (plan-write time)

- **Spec coverage:** "Replace MockProductService with ApiProductService" (spec §6.2) ✓ Tasks 1–3. Order persistence + auth = Phase 1B (separate plan).
- **Placeholders:** none — all code complete.
- **Type consistency:** `ProductDto` columns (Task 1) match `GetString`/`GetDecimal` ordinals in the reader; `IProductService` method names (`GetProductsAsync`/`GetFeaturedProductsAsync`/`GetProductByIdAsync`) match the existing interface used by `MockProductService`; client deserializes into existing `Web/Models/Product`.
- **Note:** `Product` has nullable/extra fields (`Filament`, `CustomText`) not sent by the Api — they default to null, which is correct (Studio-only fields).

## Phase 1A Done =
All 4 task commits landed; `dotnet build` green for Api + Web; `GET /api/products` returns 8 live rows; Web wired to Api.

## Next: Phase 1B — Auth + Orders
Supabase Auth (browser login → JWT), Api JWT validation middleware + role lookup, Stripe webhook → persist `orders`/`order_items` (idempotent on `stripe_session_id`), account pages (order history, saved designs).
