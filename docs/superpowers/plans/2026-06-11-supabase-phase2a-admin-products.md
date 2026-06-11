# Supabase Phase 2A — Admin Gate + Product Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Checkbox steps.
> **Execution rule (token-safety):** ONE task per turn, commit each. Resume from first unchecked box. Working tree clean between tasks.

**Goal:** An admin (a profile with `role='admin'`) can manage the catalogue from the site: list all products (including inactive), create, edit (both languages), and soft-disable them. Non-admins never see the admin UI and are rejected by the Api.

**Architecture:** Admin-only Api endpoints under `/api/admin/*` verify the caller's `profiles.role` is `admin` (server-authoritative). The browser learns the user's app role from `/api/auth/me`; the `AuthenticationStateProvider` adds it as a role claim so `<AuthorizeView Roles="admin">` and `[Authorize(Roles="admin")]` work. Storage file uploads, order management, and analytics are later sub-phases (2B/2C); admin sets image/model URLs as text for now.

**Tech Stack:** ASP.NET minimal API (.NET 7), Npgsql, Blazor WASM (Authorization).

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phases 0–1E complete. A test/admin account exists; set it admin with
`update public.profiles set role='admin' where id='<user-uuid>';`.

---

## File Structure

- Create: `src/MyMiniCar.Api/Models/AdminProductModels.cs` — `AdminProductView`, `ProductWrite`
- Modify: `src/MyMiniCar.Api/Data/ProductRepository.cs` — admin list + upsert + set-active
- Modify: `src/MyMiniCar.Api/Program.cs` — admin endpoints + admin check helper
- Modify: `src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs` — add app-role claim from `/api/auth/me`
- Modify: `src/MyMiniCar.Web/Program.cs` — give the auth provider the api base url
- Create: `src/MyMiniCar.Web/Pages/Admin/Products.razor` — admin product manager (role-gated)
- Modify: `src/MyMiniCar.Web/Shared/NavMenu.razor` — "Admin" link for admins only

---

## Task 1: Api — admin product endpoints

**Files:**
- Create: `src/MyMiniCar.Api/Models/AdminProductModels.cs`
- Modify: `src/MyMiniCar.Api/Data/ProductRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs`

- [x] **Step 1: Create the admin product models**

`src/MyMiniCar.Api/Models/AdminProductModels.cs`:
```csharp
namespace MyMiniCar.Api.Models;

/// <summary>Full product row for the admin UI (includes inactive + sort).</summary>
public sealed record AdminProductView(
    string Id, string Name, string Description, string? NameBg, string? DescriptionBg,
    decimal Price, string Category, string? ImageUrl, string? DisplayModelUrl, string? PrintModelUrl,
    string? DefaultMaterial, string? Dimensions, int WeightGrams, string TileClass,
    bool IsFeatured, bool IsActive, int SortOrder);

/// <summary>Create/update payload from the admin UI.</summary>
public sealed record ProductWrite(
    string Id, string Name, string Description, string? NameBg, string? DescriptionBg,
    decimal Price, string Category, string? ImageUrl, string? DisplayModelUrl, string? PrintModelUrl,
    string? DefaultMaterial, string? Dimensions, int WeightGrams, string TileClass,
    bool IsFeatured, bool IsActive, int SortOrder);
```

- [x] **Step 2: Add admin reads/writes to ProductRepository**

Append these methods to `ProductRepository` (after `GetByIdAsync`):
```csharp
    private const string AdminColumns = @"
        id, name, description, name_bg, description_bg, price, category,
        image_url, display_model_url, print_model_url, default_material, dimensions,
        weight_grams, coalesce(tile_class,'fil-blue'), is_featured, is_active, sort_order";

    public async Task<List<AdminProductView>> GetAllAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            $"select {AdminColumns} from public.products order by sort_order, name");
        var list = new List<AdminProductView>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(MapAdmin(r));
        return list;
    }

    private static AdminProductView MapAdmin(System.Data.Common.DbDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2),
        r.IsDBNull(3) ? null : r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        r.GetDecimal(5), r.GetString(6),
        r.IsDBNull(7) ? null : r.GetString(7),
        r.IsDBNull(8) ? null : r.GetString(8),
        r.IsDBNull(9) ? null : r.GetString(9),
        r.IsDBNull(10) ? null : r.GetString(10),
        r.IsDBNull(11) ? null : r.GetString(11),
        r.GetInt32(12), r.GetString(13),
        r.GetBoolean(14), r.GetBoolean(15), r.GetInt32(16));

    /// <summary>Insert or update a product by id.</summary>
    public async Task UpsertAsync(ProductWrite p, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(@"
            insert into public.products
              (id, name, description, name_bg, description_bg, price, category,
               image_url, display_model_url, print_model_url, default_material, dimensions,
               weight_grams, tile_class, is_featured, is_active, sort_order, updated_at)
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17, now())
            on conflict (id) do update set
              name=excluded.name, description=excluded.description,
              name_bg=excluded.name_bg, description_bg=excluded.description_bg,
              price=excluded.price, category=excluded.category, image_url=excluded.image_url,
              display_model_url=excluded.display_model_url, print_model_url=excluded.print_model_url,
              default_material=excluded.default_material, dimensions=excluded.dimensions,
              weight_grams=excluded.weight_grams, tile_class=excluded.tile_class,
              is_featured=excluded.is_featured, is_active=excluded.is_active,
              sort_order=excluded.sort_order, updated_at=now()");
        cmd.Parameters.AddWithValue(p.Id);
        cmd.Parameters.AddWithValue(p.Name);
        cmd.Parameters.AddWithValue(p.Description);
        cmd.Parameters.AddWithValue((object?)p.NameBg ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.DescriptionBg ?? DBNull.Value);
        cmd.Parameters.AddWithValue(p.Price);
        cmd.Parameters.AddWithValue(p.Category);
        cmd.Parameters.AddWithValue((object?)p.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.DisplayModelUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.PrintModelUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.DefaultMaterial ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.Dimensions ?? DBNull.Value);
        cmd.Parameters.AddWithValue(p.WeightGrams);
        cmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(p.TileClass) ? "fil-blue" : p.TileClass);
        cmd.Parameters.AddWithValue(p.IsFeatured);
        cmd.Parameters.AddWithValue(p.IsActive);
        cmd.Parameters.AddWithValue(p.SortOrder);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> SetActiveAsync(string id, bool active, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "update public.products set is_active=$2, updated_at=now() where id=$1");
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(active);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
```
NOTE: add `using System.Data.Common;` is unnecessary — the helper uses the fully-qualified `System.Data.Common.DbDataReader`.

- [x] **Step 3: Add an admin check + endpoints to Program.cs**

Add a local helper near the top of the endpoint section (after `var app = builder.Build();` block, before the endpoints), as a static local function at file scope is not possible in top-level statements that need DI — instead inline the check in each endpoint via `ProfileRepository`. Add the admin endpoints near the product endpoints:
```csharp
app.MapGet("/api/admin/products", async (ClaimsPrincipal user, ProfileRepository profiles, ProductRepository products) =>
{
    var guard = await RequireAdmin(user, profiles);
    if (guard is not null) return guard;
    return Results.Ok(await products.GetAllAsync());
}).RequireAuthorization();

app.MapPost("/api/admin/products", async (ProductWrite body, ClaimsPrincipal user, ProfileRepository profiles, ProductRepository products) =>
{
    var guard = await RequireAdmin(user, profiles);
    if (guard is not null) return guard;
    if (string.IsNullOrWhiteSpace(body.Id) || string.IsNullOrWhiteSpace(body.Name))
        return Results.BadRequest(new { error = "Id and Name are required." });
    await products.UpsertAsync(body);
    return Results.Ok(new { id = body.Id });
}).RequireAuthorization();

app.MapPost("/api/admin/products/{id}/active", async (string id, bool active, ClaimsPrincipal user, ProfileRepository profiles, ProductRepository products) =>
{
    var guard = await RequireAdmin(user, profiles);
    if (guard is not null) return guard;
    return await products.SetActiveAsync(id, active) ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();
```
Add this local function just above the admin endpoints (top-level statements allow local functions):
```csharp
// Returns null if the caller is an admin, otherwise the IResult to short-circuit with.
async Task<IResult?> RequireAdmin(ClaimsPrincipal user, ProfileRepository profiles)
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (sub is null || !Guid.TryParse(sub, out var userId)) return Results.Unauthorized();
    var role = await profiles.GetRoleAsync(userId);
    return role == "admin" ? null : Results.Forbid();
}
```

- [x] **Step 4: Build + smoke**

Run:
```bash
cd src/MyMiniCar.Api && dotnet build
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
curl -s -o /dev/null -w "admin-anon=%{http_code}\n" http://localhost:5230/api/admin/products
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: 0 errors; `admin-anon=401`.

- [x] **Step 5: Commit**

```bash
git add src/MyMiniCar.Api/Models/AdminProductModels.cs src/MyMiniCar.Api/Data/ProductRepository.cs src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): admin product endpoints (role-gated CRUD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Web — app role in auth state

**Files:**
- Modify: `src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`

- [x] **Step 1: Fetch the app role and add it as a role claim**

Replace `src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs` with:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MyMiniCar.Web.Services;

namespace MyMiniCar.Web.Auth;

/// <summary>Derives Blazor auth state from the JWT, enriched with the app role
/// (profiles.role) fetched once per token from /api/auth/me.</summary>
public sealed class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokens;
    private readonly HttpClient _http;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _cachedToken;
    private string _cachedRole = "customer";

    public SupabaseAuthStateProvider(string apiBaseUrl, TokenStore tokens)
    {
        _tokens = tokens;
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        JwtSecurityToken jwt;
        try { jwt = new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch { return Anonymous; }
        if (jwt.ValidTo < DateTime.UtcNow) return Anonymous;

        var role = await GetAppRoleAsync(token);
        var claims = new List<Claim>(jwt.Claims) { new(ClaimTypes.Role, role) };
        var identity = new ClaimsIdentity(claims, "supabase", "email", ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private async Task<string> GetAppRoleAsync(string token)
    {
        if (token == _cachedToken) return _cachedRole;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var me = await resp.Content.ReadFromJsonAsync<MeResponse>();
                _cachedRole = me?.Role ?? "customer";
                _cachedToken = token;
            }
        }
        catch { /* keep previous role on transient failure */ }
        return _cachedRole;
    }

    public void NotifyChanged()
    {
        _cachedToken = null; // force a role refresh on next evaluation
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private sealed record MeResponse(string Id, string? Email, string Role);
}
```

- [x] **Step 2: Pass the api base url in DI**

In `src/MyMiniCar.Web/Program.cs`, update the provider registration to pass `apiBaseUrl`:
```csharp
builder.Services.AddScoped<SupabaseAuthStateProvider>(
    sp => new SupabaseAuthStateProvider(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
```
(Leave the `AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SupabaseAuthStateProvider>())` line as-is.)

- [x] **Step 3: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
git add src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs src/MyMiniCar.Web/Program.cs
git commit -m "feat(web): enrich auth state with app role from /api/auth/me

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Web — admin product manager page + nav

**Files:**
- Create: `src/MyMiniCar.Web/Services/AdminProductService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`
- Create: `src/MyMiniCar.Web/Pages/Admin/Products.razor`
- Modify: `src/MyMiniCar.Web/Shared/NavMenu.razor`

- [x] **Step 1: Create the admin product service**

`src/MyMiniCar.Web/Services/AdminProductService.cs`:
```csharp
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
```

- [x] **Step 2: Register the service**

In `src/MyMiniCar.Web/Program.cs`, next to `DesignsService`:
```csharp
builder.Services.AddScoped(sp => new AdminProductService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
```

- [x] **Step 3: Create the admin page**

`src/MyMiniCar.Web/Pages/Admin/Products.razor`:
```razor
@page "/admin/products"
@attribute [Authorize(Roles = "admin")]
@using MyMiniCar.Web.Services
@inject AdminProductService Admin

<PageTitle>Admin · Products</PageTitle>
<div class="admin-page container py-5">
    <h1>Products</h1>

    @if (_products is null)
    {
        <p>Loading…</p>
    }
    else
    {
        <table class="admin-table">
            <thead><tr><th>Id</th><th>Name</th><th>Price</th><th>Active</th><th></th></tr></thead>
            <tbody>
                @foreach (var p in _products)
                {
                    <tr>
                        <td>@p.Id</td>
                        <td>@p.Name</td>
                        <td>@p.Price.ToString("0.00")</td>
                        <td>@(p.IsActive ? "✓" : "—")</td>
                        <td>
                            <button class="btn-outline-c btn-sm-c" @onclick="() => Edit(p)">Edit</button>
                            <button class="btn-outline-c btn-sm-c" @onclick="() => Toggle(p)">@(p.IsActive ? "Disable" : "Enable")</button>
                        </td>
                    </tr>
                }
            </tbody>
        </table>

        <h2 class="mt-4">@(_editing.Id == "" ? "New product" : $"Edit {_editing.Id}")</h2>
        <div class="admin-form">
            <input placeholder="id (slug)" @bind="_editing.Id" />
            <input placeholder="Name (EN)" @bind="_editing.Name" />
            <input placeholder="Name (BG)" @bind="_editing.NameBg" />
            <textarea placeholder="Description (EN)" @bind="_editing.Description"></textarea>
            <textarea placeholder="Description (BG)" @bind="_editing.DescriptionBg"></textarea>
            <input type="number" step="0.01" placeholder="Price" @bind="_editing.Price" />
            <input placeholder="Category" @bind="_editing.Category" />
            <input placeholder="Image URL" @bind="_editing.ImageUrl" />
            <input placeholder="Dimensions" @bind="_editing.Dimensions" />
            <input placeholder="Default material" @bind="_editing.DefaultMaterial" />
            <input placeholder="Tile class" @bind="_editing.TileClass" />
            <input type="number" placeholder="Weight (g)" @bind="_editing.WeightGrams" />
            <input type="number" placeholder="Sort order" @bind="_editing.SortOrder" />
            <label><input type="checkbox" @bind="_editing.IsFeatured" /> Featured</label>
            <label><input type="checkbox" @bind="_editing.IsActive" /> Active</label>
            <div class="d-flex gap-2">
                <button class="btn-accent-c" @onclick="Save" disabled="@_saving">Save</button>
                <button class="btn-outline-c" @onclick="NewProduct">New</button>
            </div>
            @if (_message is not null) { <p>@_message</p> }
        </div>
    }
</div>

@code {
    private List<AdminProduct>? _products;
    private AdminProduct _editing = new();
    private bool _saving;
    private string? _message;

    protected override async Task OnInitializedAsync() => await Load();

    private async Task Load() => _products = await Admin.GetAllAsync();

    private void Edit(AdminProduct p) => _editing = new AdminProduct
    {
        Id = p.Id, Name = p.Name, NameBg = p.NameBg, Description = p.Description,
        DescriptionBg = p.DescriptionBg, Price = p.Price, Category = p.Category,
        ImageUrl = p.ImageUrl, DisplayModelUrl = p.DisplayModelUrl, PrintModelUrl = p.PrintModelUrl,
        DefaultMaterial = p.DefaultMaterial, Dimensions = p.Dimensions, WeightGrams = p.WeightGrams,
        TileClass = p.TileClass, IsFeatured = p.IsFeatured, IsActive = p.IsActive, SortOrder = p.SortOrder
    };

    private void NewProduct() => _editing = new AdminProduct();

    private async Task Save()
    {
        _saving = true; _message = null;
        var ok = await Admin.SaveAsync(_editing);
        _message = ok ? "Saved." : "Save failed.";
        if (ok) { await Load(); NewProduct(); }
        _saving = false;
    }

    private async Task Toggle(AdminProduct p)
    {
        if (await Admin.SetActiveAsync(p.Id, !p.IsActive)) await Load();
    }
}
```

- [x] **Step 4: Add an admin nav link (admins only)**

In `src/MyMiniCar.Web/Shared/NavMenu.razor`, inside the `<Authorized>` block (after "My orders"):
```razor
                    <AuthorizeView Roles="admin" Context="adminCtx">
                        <a href="admin/products" class="nav-link nav-link-mmc d-none d-sm-inline" @onclick="CloseNavMenu">Admin</a>
                    </AuthorizeView>
```
NOTE: the outer `<AuthorizeView>` already uses `context`; the nested one needs its own `Context="adminCtx"` to avoid a name clash.

- [x] **Step 5: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [x] **Step 6: Commit**

```bash
git add src/MyMiniCar.Web/Services/AdminProductService.cs src/MyMiniCar.Web/Program.cs src/MyMiniCar.Web/Pages/Admin/Products.razor src/MyMiniCar.Web/Shared/NavMenu.razor
git commit -m "feat(web): admin product manager page (role-gated)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Live verify (admin vs non-admin)

**Files:** none.

- [ ] **Step 1: Make a verified admin + a normal user, test the gate**

```bash
cd src/MyMiniCar.Api
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
URL="https://sdiirjjagjqqvqfexohq.supabase.co"; ANON="<anon-key>"
# normal user
EU="mmc.norm$(date +%s)@gmail.com"
curl -s -X POST "$URL/auth/v1/signup" -H "apikey: $ANON" -H "Content-Type: application/json" -d "{\"email\":\"$EU\",\"password\":\"TestPass123!\"}" >/dev/null
NT=$(curl -s -X POST "$URL/auth/v1/token?grant_type=password" -H "apikey: $ANON" -H "Content-Type: application/json" -d "{\"email\":\"$EU\",\"password\":\"TestPass123!\"}" | python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")
echo "normal -> admin/products:"; curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $NT" http://localhost:5230/api/admin/products  # expect 403
# admin user
EA="mmc.admin$(date +%s)@gmail.com"
curl -s -X POST "$URL/auth/v1/signup" -H "apikey: $ANON" -H "Content-Type: application/json" -d "{\"email\":\"$EA\",\"password\":\"TestPass123!\"}" >/dev/null
AT=$(curl -s -X POST "$URL/auth/v1/token?grant_type=password" -H "apikey: $ANON" -H "Content-Type: application/json" -d "{\"email\":\"$EA\",\"password\":\"TestPass123!\"}" | python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")
ASUB=$(python3 -c "import base64,json,sys;t='$AT'.split('.')[1];t+='='*(-len(t)%4);print(json.loads(base64.urlsafe_b64decode(t))['sub'])")
export PGPASSWORD='<db-password>'
psql "host=aws-1-eu-north-1.pooler.supabase.com port=5432 dbname=postgres user=postgres.sdiirjjagjqqvqfexohq sslmode=require" -c "update public.profiles set role='admin' where id='$ASUB';"
echo "admin -> admin/products:"; curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $AT" http://localhost:5230/api/admin/products  # expect 200
echo "admin list count:"; curl -s -H "Authorization: Bearer $AT" http://localhost:5230/api/admin/products | python3 -c "import sys,json;print(len(json.load(sys.stdin)))"
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: normal `403`, admin `200`, list count `8`.

- [ ] **Step 2: Commit plan complete**

Tick all boxes, commit the plan file.

---

## Self-Review (plan-write time)

- **Spec coverage:** admin product CRUD + admin gate (spec §6.2 admin console) ✓. Storage uploads = 2B; order management = 2B; analytics = 2C.
- **Placeholders:** none — all code complete. `<anon-key>`/`<db-password>` are run-time secrets.
- **Type consistency:** `ProductWrite`/`AdminProductView` (Api) ↔ `AdminProduct` (Web) share field names for JSON; `RequireAdmin` returns `IResult?`; role claim uses `ClaimTypes.Role` so `[Authorize(Roles="admin")]` + `<AuthorizeView Roles="admin">` match.
- **Security:** every `/api/admin/*` endpoint calls `RequireAdmin` (server-authoritative); client role is convenience only.

## Phase 2A Done =
Both build green; `/api/admin/products` 401 anon / 403 normal / 200 admin; admin page lists + edits + toggles products; "Admin" nav shows only for admins.

## Next: Phase 2B — Storage uploads + order management
Supabase Storage buckets (product-images, models) + admin upload UI; admin order list + status updates (paid→shipped→delivered). Then 2C analytics.
