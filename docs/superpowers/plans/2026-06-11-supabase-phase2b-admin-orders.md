# Supabase Phase 2B — Admin Order Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Checkbox steps.
> **Execution rule (token-safety):** ONE task per turn, commit each. Resume from first unchecked box. Working tree clean between tasks.

**Goal:** An admin can see every order and advance its status (paid → shipped → delivered, or cancel/refund) from an admin page.

**Architecture:** Admin-only endpoints reuse the `RequireAdmin` gate from Phase 2A. `OrderRepository` gains an all-orders read and a status update. A `/admin/orders` page lists orders with their items and a status selector. Storage uploads = Phase 2C; analytics = Phase 2D.

**Tech Stack:** ASP.NET minimal API (.NET 7), Npgsql, Blazor WASM (Authorization).

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phases 0–2A complete (admin gate, role-aware auth state).

---

## File Structure

- Modify: `src/MyMiniCar.Api/Models/OrderModels.cs` — `AdminOrderView`
- Modify: `src/MyMiniCar.Api/Data/OrderRepository.cs` — `GetAllAsync`, `UpdateStatusAsync`
- Modify: `src/MyMiniCar.Api/Program.cs` — `GET /api/admin/orders`, `POST /api/admin/orders/{id}/status`
- Create: `src/MyMiniCar.Web/Services/AdminOrdersService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs` — register the service
- Create: `src/MyMiniCar.Web/Pages/Admin/Orders.razor`
- Modify: `src/MyMiniCar.Web/Pages/Admin/Products.razor` — add a link to Orders (simple admin nav)

---

## Task 1: Api — admin order endpoints

**Files:**
- Modify: `src/MyMiniCar.Api/Models/OrderModels.cs`
- Modify: `src/MyMiniCar.Api/Data/OrderRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs`

- [x] **Step 1: Add AdminOrderView to OrderModels.cs**

Append to `src/MyMiniCar.Api/Models/OrderModels.cs`:
```csharp
/// <summary>An order with customer + shipping summary for the admin view.</summary>
public sealed record AdminOrderView(
    Guid Id,
    string Status,
    string? Email,
    string? CustomerName,
    decimal Total,
    string Currency,
    string? Carrier,
    string? ShippingMethod,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemView> Items);
```

- [x] **Step 2: Add GetAllAsync + UpdateStatusAsync to OrderRepository**

Append to `OrderRepository` (after `GetByUserAsync`):
```csharp
    /// <summary>All orders (newest first) with line items, for the admin view.</summary>
    public async Task<List<AdminOrderView>> GetAllAsync(CancellationToken ct = default)
    {
        var heads = new List<(Guid Id, string Status, string? Email, string? Name, decimal Total,
                              string Currency, string? Carrier, string? Method, DateTime Created)>();
        await using (var cmd = _db.DataSource.CreateCommand(@"
            select id, status, email, customer_name, total, currency, carrier, shipping_method, created_at
            from public.orders order by created_at desc"))
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                heads.Add((
                    r.GetGuid(0), r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.GetDecimal(4), r.GetString(5),
                    r.IsDBNull(6) ? null : r.GetString(6),
                    r.IsDBNull(7) ? null : r.GetString(7),
                    r.GetDateTime(8)));
        }

        var views = new List<AdminOrderView>();
        foreach (var h in heads)
        {
            var items = new List<OrderItemView>();
            await using var icmd = _db.DataSource.CreateCommand(
                "select name, unit_price, quantity from public.order_items where order_id = $1");
            icmd.Parameters.AddWithValue(h.Id);
            await using var ir = await icmd.ExecuteReaderAsync(ct);
            while (await ir.ReadAsync(ct))
                items.Add(new OrderItemView(ir.GetString(0), ir.GetDecimal(1), ir.GetInt32(2)));

            views.Add(new AdminOrderView(h.Id, h.Status, h.Email, h.Name, h.Total, h.Currency,
                                         h.Carrier, h.Method, h.Created, items));
        }
        return views;
    }

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "pending", "paid", "shipped", "delivered", "cancelled", "refunded" };

    /// <summary>Updates an order's status. Returns false if the status is invalid or no row matched.</summary>
    public async Task<bool> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        if (!AllowedStatuses.Contains(status)) return false;
        await using var cmd = _db.DataSource.CreateCommand(
            "update public.orders set status = $2 where id = $1");
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(status.ToLowerInvariant());
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
```

- [x] **Step 3: Add endpoints to Program.cs**

Next to the admin product endpoints:
```csharp
app.MapGet("/api/admin/orders", async (ClaimsPrincipal user, ProfileRepository profiles, OrderRepository orders) =>
{
    var guard = await RequireAdmin(user, profiles);
    if (guard is not null) return guard;
    return Results.Ok(await orders.GetAllAsync());
}).RequireAuthorization();

app.MapPost("/api/admin/orders/{id:guid}/status", async (Guid id, string status, ClaimsPrincipal user, ProfileRepository profiles, OrderRepository orders) =>
{
    var guard = await RequireAdmin(user, profiles);
    if (guard is not null) return guard;
    return await orders.UpdateStatusAsync(id, status) ? Results.NoContent() : Results.BadRequest(new { error = "Invalid status or order." });
}).RequireAuthorization();
```

- [x] **Step 4: Build + smoke**

Run:
```bash
cd src/MyMiniCar.Api && dotnet build
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
curl -s -o /dev/null -w "orders-anon=%{http_code}\n" http://localhost:5230/api/admin/orders
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: 0 errors; `orders-anon=401`.

- [x] **Step 5: Commit**

```bash
git add src/MyMiniCar.Api/Models/OrderModels.cs src/MyMiniCar.Api/Data/OrderRepository.cs src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): admin order list + status update (role-gated)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Web — admin orders page

**Files:**
- Create: `src/MyMiniCar.Web/Services/AdminOrdersService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`
- Create: `src/MyMiniCar.Web/Pages/Admin/Orders.razor`
- Modify: `src/MyMiniCar.Web/Pages/Admin/Products.razor`

- [x] **Step 1: Create the service**

`src/MyMiniCar.Web/Services/AdminOrdersService.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

public sealed class AdminOrdersService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public AdminOrdersService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<List<AdminOrder>?> GetAllAsync()
    {
        var req = await AuthedAsync(HttpMethod.Get, "/api/admin/orders");
        if (req is null) return null;
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<List<AdminOrder>>() : null;
    }

    public async Task<bool> SetStatusAsync(Guid id, string status)
    {
        var req = await AuthedAsync(HttpMethod.Post, $"/api/admin/orders/{id}/status?status={status}");
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

public sealed record AdminOrderItem(string Name, decimal UnitPrice, int Quantity);
public sealed record AdminOrder(
    Guid Id, string Status, string? Email, string? CustomerName, decimal Total, string Currency,
    string? Carrier, string? ShippingMethod, DateTime CreatedAt, List<AdminOrderItem> Items);
```

- [x] **Step 2: Register the service**

In `src/MyMiniCar.Web/Program.cs`, next to `AdminProductService`:
```csharp
builder.Services.AddScoped(sp => new AdminOrdersService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
```

- [x] **Step 3: Create the orders page**

`src/MyMiniCar.Web/Pages/Admin/Orders.razor`:
```razor
@page "/admin/orders"
@attribute [Authorize(Roles = "admin")]
@using MyMiniCar.Web.Services
@inject AdminOrdersService Admin

<PageTitle>Admin · Orders</PageTitle>
<div class="admin-page container py-5">
    <div class="d-flex gap-3 mb-3">
        <a href="admin/products" class="nav-link-mmc">Products</a>
        <a href="admin/orders" class="nav-link-mmc">Orders</a>
    </div>
    <h1>Orders</h1>

    @if (_orders is null)
    {
        <p>Loading…</p>
    }
    else if (_orders.Count == 0)
    {
        <p>No orders yet.</p>
    }
    else
    {
        <table class="admin-table">
            <thead><tr><th>Date</th><th>Customer</th><th>Items</th><th>Total</th><th>Status</th></tr></thead>
            <tbody>
                @foreach (var o in _orders)
                {
                    <tr>
                        <td>@o.CreatedAt.ToLocalTime().ToString("dd MMM HH:mm")</td>
                        <td>@(o.CustomerName ?? o.Email ?? "Guest")</td>
                        <td>@string.Join(", ", o.Items.Select(i => $"{i.Quantity}× {i.Name}"))</td>
                        <td>@o.Total.ToString("0.00") @o.Currency.ToUpperInvariant()</td>
                        <td>
                            <select value="@o.Status" @onchange="e => ChangeStatus(o, e.Value?.ToString())">
                                @foreach (var s in _statuses)
                                {
                                    <option value="@s" selected="@(s == o.Status)">@s</option>
                                }
                            </select>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    }
</div>

@code {
    private List<AdminOrder>? _orders;
    private readonly string[] _statuses = { "pending", "paid", "shipped", "delivered", "cancelled", "refunded" };

    protected override async Task OnInitializedAsync() => _orders = await Admin.GetAllAsync();

    private async Task ChangeStatus(AdminOrder o, string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status == o.Status) return;
        if (await Admin.SetStatusAsync(o.Id, status))
            _orders = await Admin.GetAllAsync();
    }
}
```

- [x] **Step 4: Add an Orders link on the Products admin page**

In `src/MyMiniCar.Web/Pages/Admin/Products.razor`, right after the opening `<div class="admin-page container py-5">`, add a small admin nav:
```razor
    <div class="d-flex gap-3 mb-3">
        <a href="admin/products" class="nav-link-mmc">Products</a>
        <a href="admin/orders" class="nav-link-mmc">Orders</a>
    </div>
```

- [x] **Step 5: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [x] **Step 6: Commit**

```bash
git add src/MyMiniCar.Web/Services/AdminOrdersService.cs src/MyMiniCar.Web/Program.cs src/MyMiniCar.Web/Pages/Admin/Orders.razor src/MyMiniCar.Web/Pages/Admin/Products.razor
git commit -m "feat(web): admin order management page (list + status)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Live verify

**Files:** none.

- [ ] **Step 1: Admin lists orders + updates a status**

Use the admin account (`mymincar.admin@gmail.com` / `MmcAdmin2026!`):
```bash
cd src/MyMiniCar.Api
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
URL="https://sdiirjjagjqqvqfexohq.supabase.co"; ANON="<anon-key>"
AT=$(curl -s -X POST "$URL/auth/v1/token?grant_type=password" -H "apikey: $ANON" -H "Content-Type: application/json" -d '{"email":"mymincar.admin@gmail.com","password":"MmcAdmin2026!"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")
echo "list:"; curl -s -H "Authorization: Bearer $AT" http://localhost:5230/api/admin/orders | python3 -c "import sys,json;d=json.load(sys.stdin);print('count=',len(d)); print('first=',d[0]['status'], d[0]['total']) if d else None"
OID=$(curl -s -H "Authorization: Bearer $AT" http://localhost:5230/api/admin/orders | python3 -c "import sys,json;d=json.load(sys.stdin);print(d[0]['id'] if d else '')")
echo "set shipped:"; curl -s -o /dev/null -w "%{http_code}\n" -X POST "http://localhost:5230/api/admin/orders/$OID/status?status=shipped" -H "Authorization: Bearer $AT"
echo "verify:"; curl -s -H "Authorization: Bearer $AT" http://localhost:5230/api/admin/orders | python3 -c "import sys,json;d=json.load(sys.stdin);print('first status=', d[0]['status'] if d else None)"
echo "bad status:"; curl -s -o /dev/null -w "%{http_code}\n" -X POST "http://localhost:5230/api/admin/orders/$OID/status?status=bogus" -H "Authorization: Bearer $AT"
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: list `count>=1`; set shipped `204`; verify `first status= shipped`; bad status `400`.

- [ ] **Step 2: Commit plan complete**

Tick all boxes, commit the plan file.

---

## Self-Review (plan-write time)

- **Spec coverage:** admin order management — view orders + update status (spec §6.2) ✓. Storage uploads = 2C; analytics = 2D.
- **Placeholders:** none — all code complete.
- **Type consistency:** `AdminOrderView` (Api) ↔ `AdminOrder` (Web) share field names; `OrderItemView` already exists in Api; `AdminOrderItem` mirrors it on the Web; statuses validated server-side against `AllowedStatuses`.
- **Security:** both endpoints call `RequireAdmin`; status whitelisted in the repo.

## Phase 2B Done =
Both build green; `/api/admin/orders` 401 anon / 200 admin; status update 204 valid / 400 invalid; `/admin/orders` lists orders with a working status selector.

## Next: Phase 2C — Storage uploads
Supabase Storage buckets (product-images, models) + admin upload of image/model files (replacing manual URLs). Then Phase 2D analytics dashboards.
