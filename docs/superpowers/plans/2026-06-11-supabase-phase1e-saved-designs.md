# Supabase Phase 1E — Saved Studio Designs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Checkbox steps.
> **Execution rule (token-safety):** ONE task per turn, commit each. Resume from first unchecked box. Working tree clean between tasks.

**Goal:** Logged-in users can save a Studio configuration to their account, see their saved designs on `/account`, load one back into the Studio, and delete it.

**Architecture:** The `designs` table (created in Phase 0) stores one row per saved design: `user_id`, `name`, `config` (jsonb), `source`. The Api treats `config` as an opaque JSON blob (store/return as text) — the browser owns its shape. The Studio serializes its state (car key + body colour + `StudioConfig` fields) into that blob on save, and rehydrates from it on load (`/convert?design=<id>`). All design endpoints are `[Authorize]` and owner-scoped.

**Tech Stack:** ASP.NET minimal API (.NET 7, JwtBearer/JWKS), Npgsql, Blazor WASM (Authorization).

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phases 0–1D complete (DB, auth via JWKS, account page).

---

## File Structure

- Modify: `src/MyMiniCar.Api/Models/OrderModels.cs` is unrelated — instead Create: `src/MyMiniCar.Api/Models/DesignModels.cs`
- Create: `src/MyMiniCar.Api/Data/DesignRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs` — register repo + 3 design endpoints
- Create: `src/MyMiniCar.Web/Services/DesignsService.cs` — authed save/list/delete + DTOs
- Modify: `src/MyMiniCar.Web/Program.cs` — register `DesignsService`
- Modify: `src/MyMiniCar.Web/Pages/Convert.razor` — Save button (authed) + load-from-query
- Modify: `src/MyMiniCar.Web/Pages/Account.razor` — "My designs" list with Load + Delete

---

## Task 1: Api — DesignRepository + endpoints

**Files:**
- Create: `src/MyMiniCar.Api/Models/DesignModels.cs`
- Create: `src/MyMiniCar.Api/Data/DesignRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs`

- [ ] **Step 1: Create the models**

`src/MyMiniCar.Api/Models/DesignModels.cs`:
```csharp
namespace MyMiniCar.Api.Models;

/// <summary>A design to save. Config is an opaque JSON blob owned by the client.</summary>
public sealed record DesignCreate(string? Name, string ConfigJson);

/// <summary>A saved design returned to the account/studio.</summary>
public sealed record DesignView(Guid Id, string? Name, string Config, DateTime CreatedAt);
```

- [ ] **Step 2: Create the repository**

`src/MyMiniCar.Api/Data/DesignRepository.cs`:
```csharp
using MyMiniCar.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace MyMiniCar.Api.Data;

/// <summary>Owner-scoped CRUD for saved Studio designs. config is stored as jsonb.</summary>
public sealed class DesignRepository
{
    private readonly SupabaseDataSource _db;

    public DesignRepository(SupabaseDataSource db) => _db = db;

    public async Task<Guid> CreateAsync(Guid userId, DesignCreate input, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(@"
            insert into public.designs (user_id, name, config, source)
            values ($1, $2, $3::jsonb, 'studio')
            returning id");
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue((object?)input.Name ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter { Value = input.ConfigJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<List<DesignView>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "select id, name, config::text, created_at from public.designs where user_id = $1 order by created_at desc");
        cmd.Parameters.AddWithValue(userId);

        var list = new List<DesignView>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new DesignView(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3)));
        return list;
    }

    /// <summary>Deletes a design only if it belongs to the user. Returns true if a row was removed.</summary>
    public async Task<bool> DeleteAsync(Guid userId, Guid designId, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "delete from public.designs where id = $1 and user_id = $2");
        cmd.Parameters.AddWithValue(designId);
        cmd.Parameters.AddWithValue(userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
```

- [ ] **Step 3: Register repo + endpoints in Program.cs**

Next to the other repo registrations add:
```csharp
builder.Services.AddScoped<DesignRepository>();
```
Add endpoints near `/api/orders/mine` (all require auth). They share a small helper to read the user id from claims:
```csharp
app.MapGet("/api/designs/mine", async (ClaimsPrincipal user, DesignRepository designs) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (sub is null || !Guid.TryParse(sub, out var userId)) return Results.Unauthorized();
    return Results.Ok(await designs.GetByUserAsync(userId));
}).RequireAuthorization();

app.MapPost("/api/designs", async (DesignCreate body, ClaimsPrincipal user, DesignRepository designs) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (sub is null || !Guid.TryParse(sub, out var userId)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(body.ConfigJson)) return Results.BadRequest(new { error = "Empty config." });
    var id = await designs.CreateAsync(userId, body);
    return Results.Ok(new { id });
}).RequireAuthorization();

app.MapDelete("/api/designs/{id:guid}", async (Guid id, ClaimsPrincipal user, DesignRepository designs) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (sub is null || !Guid.TryParse(sub, out var userId)) return Results.Unauthorized();
    return await designs.DeleteAsync(userId, id) ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();
```

- [ ] **Step 4: Build + smoke (auth required)**

Run:
```bash
cd src/MyMiniCar.Api && dotnet build
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
curl -s -o /dev/null -w "designs-anon=%{http_code}\n" http://localhost:5230/api/designs/mine
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: 0 build errors; `designs-anon=401`.

- [ ] **Step 5: Commit**

```bash
git add src/MyMiniCar.Api/Models/DesignModels.cs src/MyMiniCar.Api/Data/DesignRepository.cs src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): saved-design endpoints (owner-scoped CRUD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Web — DesignsService

**Files:**
- Create: `src/MyMiniCar.Web/Services/DesignsService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`

- [ ] **Step 1: Create the service + DTOs**

`src/MyMiniCar.Web/Services/DesignsService.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

/// <summary>Saved Studio designs via the Api (bearer-authed).</summary>
public sealed class DesignsService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public DesignsService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<bool> SaveAsync(string? name, string configJson)
    {
        var req = await AuthedAsync(HttpMethod.Post, "/api/designs");
        if (req is null) return false;
        req.Content = JsonContent.Create(new { name, configJson });
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<DesignView>?> GetMineAsync()
    {
        var req = await AuthedAsync(HttpMethod.Get, "/api/designs/mine");
        if (req is null) return null;
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<List<DesignView>>();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var req = await AuthedAsync(HttpMethod.Delete, $"/api/designs/{id}");
        if (req is null) return false;
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode;
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

public sealed record DesignView(Guid Id, string? Name, string Config, DateTime CreatedAt);
```

- [ ] **Step 2: Register it in Program.cs**

Next to the `OrdersService` registration:
```csharp
builder.Services.AddScoped(sp => new DesignsService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
```

- [ ] **Step 3: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MyMiniCar.Web/Services/DesignsService.cs src/MyMiniCar.Web/Program.cs
git commit -m "feat(web): DesignsService (authed save/list/delete)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Studio save/load + account designs list

**Files:**
- Modify: `src/MyMiniCar.Web/Pages/Convert.razor`
- Modify: `src/MyMiniCar.Web/Pages/Account.razor`

- [ ] **Step 1: Add a saved-config shape + Save button + load to Convert.razor**

At the top of `src/MyMiniCar.Web/Pages/Convert.razor`, after the existing `@inject` lines, add:
```razor
@inject MyMiniCar.Web.Services.DesignsService Designs
@inject NavigationManager Nav
@using System.Text.Json
@using MyMiniCar.Web.Services
```
In the `studio-bar` actions `<div class="d-flex align-items-center gap-3">`, add a Save button shown only when logged in (before the price span):
```razor
            <AuthorizeView>
                <Authorized>
                    <button class="btn-outline-c" @onclick="SaveDesign" disabled="@_saving">
                        <i class="bi bi-bookmark-plus"></i> @(_saved ? @Language.T("Saved") : @Language.T("Save design"))
                    </button>
                </Authorized>
            </AuthorizeView>
```
Add a query parameter + the save/load logic to the `@code` block:
```csharp
    [Parameter, SupplyParameterFromQuery(Name = "design")]
    public string? DesignId { get; set; }

    private bool _saving;
    private bool _saved;

    private sealed record SavedConfig(
        string CarKey, string BodyColor, string SizeKey,
        string MaterialName, string TemplateId, string Line1, string Line2);

    protected override async Task OnInitializedAsync()
    {
        if (Guid.TryParse(DesignId, out var id))
        {
            var mine = await Designs.GetMineAsync();
            var match = mine?.FirstOrDefault(d => d.Id == id);
            if (match is not null) ApplyConfig(match.Config);
        }
    }

    private async Task SaveDesign()
    {
        _saving = true;
        var cfg = new SavedConfig(
            _car.Key, _bodyColor, _config.SizeKey,
            _config.MaterialName, _config.TemplateId, _config.Line1, _config.Line2);
        var name = $"{_car.Name} · {_config.Size.Name}";
        _saved = await Designs.SaveAsync(name, JsonSerializer.Serialize(cfg));
        _saving = false;
    }

    private void ApplyConfig(string json)
    {
        SavedConfig? cfg;
        try { cfg = JsonSerializer.Deserialize<SavedConfig>(json); }
        catch { return; }
        if (cfg is null) return;

        _car = CarCatalog.All.FirstOrDefault(c => c.Key == cfg.CarKey) ?? CarCatalog.Default;
        _bodyColor = string.IsNullOrWhiteSpace(cfg.BodyColor) ? _bodyColor : cfg.BodyColor;
        _config.SizeKey = cfg.SizeKey;
        _config.MaterialName = cfg.MaterialName;
        _config.TemplateId = cfg.TemplateId;
        _config.Line1 = cfg.Line1;
        _config.Line2 = cfg.Line2;
    }
```
NOTE: keep the existing `OnInitialized()` that subscribes to `Language.OnChange`. If the existing method is `protected override void OnInitialized()`, leave it and ADD the async `OnInitializedAsync()` above (Blazor calls both). Do not merge or delete the language subscription.

- [ ] **Step 2: Add a "My designs" section to Account.razor**

In `src/MyMiniCar.Web/Pages/Account.razor`, add the injects at the top (after the existing `@inject OrdersService Orders`):
```razor
@inject DesignsService Designs
@inject NavigationManager Nav
```
Add a designs section after the orders markup (inside `account-page`, after the orders `}` block closes):
```razor
    <h2 class="mt-5">My designs</h2>
    @if (_designs is null || _designs.Count == 0)
    {
        <p>No saved designs yet.</p>
    }
    else
    {
        <div class="design-list">
            @foreach (var d in _designs)
            {
                <div class="design-card">
                    <span class="design-name">@(d.Name ?? "Untitled")</span>
                    <span class="design-date">@d.CreatedAt.ToLocalTime().ToString("dd MMM yyyy")</span>
                    <a class="btn-outline-c btn-sm-c" href="@($"convert?design={d.Id}")">Load</a>
                    <button class="btn-outline-c btn-sm-c" @onclick="() => Delete(d.Id)">Delete</button>
                </div>
            }
        </div>
    }
```
Extend the `@code` block:
```csharp
    private List<DesignView>? _designs;

    // inside the existing OnInitializedAsync, after loading orders:
    //   _designs = await Designs.GetMineAsync();

    private async Task Delete(Guid id)
    {
        if (await Designs.DeleteAsync(id))
            _designs = await Designs.GetMineAsync();
    }
```
Concretely, replace the existing `OnInitializedAsync` in Account.razor with:
```csharp
    protected override async Task OnInitializedAsync()
    {
        _orders = await Orders.GetMineAsync();
        _designs = await Designs.GetMineAsync();
        _loading = false;
    }
```

- [ ] **Step 3: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MyMiniCar.Web/Pages/Convert.razor src/MyMiniCar.Web/Pages/Account.razor
git commit -m "feat(web): save/load Studio designs + account designs list

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Live verify (real token)

**Files:** none.

- [ ] **Step 1: Save → list → delete via the Api with a real token**

```bash
cd src/MyMiniCar.Api
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
URL="https://sdiirjjagjqqvqfexohq.supabase.co"
ANON="<anon-key>"
EMAIL="mmc.user$(date +%s)@gmail.com"
curl -s -X POST "$URL/auth/v1/signup" -H "apikey: $ANON" -H "Content-Type: application/json" -d "{\"email\":\"$EMAIL\",\"password\":\"TestPass123!\"}" >/dev/null
TOKEN=$(curl -s -X POST "$URL/auth/v1/token?grant_type=password" -H "apikey: $ANON" -H "Content-Type: application/json" -d "{\"email\":\"$EMAIL\",\"password\":\"TestPass123!\"}" | python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")
echo "save:"; curl -s -X POST http://localhost:5230/api/designs -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Test","configJson":"{\"CarKey\":\"golf\",\"BodyColor\":\"#2D6CFF\",\"SizeKey\":\"keychain\",\"MaterialName\":\"Matte PLA\",\"TemplateId\":\"plate\",\"Line1\":\"\",\"Line2\":\"\"}"}'; echo
echo "list:"; curl -s http://localhost:5230/api/designs/mine -H "Authorization: Bearer $TOKEN"; echo
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: save returns `{"id":"..."}`, list returns an array with that design.

- [ ] **Step 2: Commit plan complete**

Tick all boxes, then commit the plan file.

---

## Self-Review (plan-write time)

- **Spec coverage:** saved designs (spec §6.2 "Save-design action in Studio", account saved-designs) ✓.
- **Placeholders:** none — all code complete. `<anon-key>` in the verify step is the public key, supplied at run time.
- **Type consistency:** `DesignView` defined identically in Api (`DesignModels.cs`) and Web (`DesignsService.cs`); the client `SavedConfig` shape is opaque to the Api (stored/returned as jsonb text); `Convert.razor` reads `_car.Key` / `_config` fields that already exist.
- **Auth:** all endpoints `RequireAuthorization`; delete is owner-scoped (`where user_id = $2`); Save button gated behind `<AuthorizeView>`.

## Phase 1E Done =
Both projects build green; design endpoints 401 anon; save/list/delete works with a real token; Studio shows Save when logged in; `/account` lists designs with Load + Delete. Completes Phase 1 (persistence + auth).

## Next: Phase 2 — Admin console
Product CRUD + Storage image/model uploads, order management (status updates), analytics dashboards. Requires an admin role (`update profiles set role='admin'` for your user).
