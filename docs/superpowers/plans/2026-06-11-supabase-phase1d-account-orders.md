# Supabase Phase 1D — Account & Order History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Checkbox steps.
> **Execution rule (token-safety):** ONE task per turn, commit each. Resume from first unchecked box. Working tree clean between tasks.

**Goal:** Logged-in customers see their order history, and new orders are linked to their account. Guest checkout still works (orders with null `user_id`).

**Architecture:** When a logged-in buyer starts checkout, the browser sends its JWT; the Api stamps the user id into the Stripe session metadata; the webhook persists it onto `orders.user_id`. A new authorized endpoint `GET /api/orders/mine` returns the caller's orders; a protected `/account` page lists them. Saved designs are a separate later phase (1E).

**Tech Stack:** ASP.NET minimal API (.NET 7, JwtBearer), Npgsql, Blazor WASM (Authorization).

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phase 0/1A/1B/1C complete (DB live, orders webhook, auth).

---

## File Structure

- Modify: `src/MyMiniCar.Api/Models/OrderModels.cs` — add `UserId` to `PaidOrderInput`; add read DTOs
- Modify: `src/MyMiniCar.Api/Data/OrderRepository.cs` — persist `user_id`; `GetByUserAsync`
- Modify: `src/MyMiniCar.Api/Program.cs` — stamp `user_id` in create-session; read it in webhook; `GET /api/orders/mine`
- Modify: `src/MyMiniCar.Web/Services/CheckoutService.cs` — attach bearer token when logged in
- Create: `src/MyMiniCar.Web/Services/OrdersService.cs` — authorized `GET /api/orders/mine`
- Modify: `src/MyMiniCar.Web/Program.cs` — register `OrdersService`; give `CheckoutService` the `TokenStore`
- Create: `src/MyMiniCar.Web/Pages/Account.razor` — protected order-history page
- Modify: `src/MyMiniCar.Web/Shared/NavMenu.razor` — "My orders" link when authed

---

## Task 1: OrderRepository — persist user_id + read orders

**Files:**
- Modify: `src/MyMiniCar.Api/Models/OrderModels.cs`
- Modify: `src/MyMiniCar.Api/Data/OrderRepository.cs`

- [x] **Step 1: Add UserId to PaidOrderInput + read DTOs**

In `src/MyMiniCar.Api/Models/OrderModels.cs`, add `UserId` as the first member of `PaidOrderInput`:
```csharp
public sealed record PaidOrderInput(
    Guid? UserId,
    string StripeSessionId,
    string? Email,
    string? CustomerName,
    string? CustomerPhone,
    decimal Subtotal,
    decimal ShippingAmount,
    decimal Total,
    string Currency,
    string? Carrier,
    string? ShippingMethod,
    string ShippingJson,
    IReadOnlyList<OrderLineInput> Items);
```
Append read DTOs to the same file:
```csharp
/// <summary>One line of a past order, returned to the account page.</summary>
public sealed record OrderItemView(string Name, decimal UnitPrice, int Quantity);

/// <summary>An order summary for the account/order-history page.</summary>
public sealed record OrderView(
    Guid Id,
    string Status,
    decimal Total,
    string Currency,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemView> Items);
```

- [x] **Step 2: Persist user_id in the insert**

In `src/MyMiniCar.Api/Data/OrderRepository.cs`, change the insert to include `user_id`. Replace the insert command's column list/values + first parameter:
```csharp
        await using (var cmd = new NpgsqlCommand(@"
            insert into public.orders
              (user_id, stripe_session_id, status, email, customer_name, customer_phone,
               subtotal, shipping_amount, total, currency, carrier, shipping_method, shipping, paid_at)
            values ($1,$2,'paid',$3,$4,$5,$6,$7,$8,$9,$10,$11,$12::jsonb, now())
            on conflict (stripe_session_id) do nothing
            returning id", conn, tx))
        {
            cmd.Parameters.AddWithValue((object?)input.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue(input.StripeSessionId);
            cmd.Parameters.AddWithValue((object?)input.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)input.CustomerName ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)input.CustomerPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue(input.Subtotal);
            cmd.Parameters.AddWithValue(input.ShippingAmount);
            cmd.Parameters.AddWithValue(input.Total);
            cmd.Parameters.AddWithValue(input.Currency);
            cmd.Parameters.AddWithValue((object?)input.Carrier ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)input.ShippingMethod ?? DBNull.Value);
            cmd.Parameters.Add(new NpgsqlParameter { Value = input.ShippingJson, NpgsqlDbType = NpgsqlDbType.Jsonb });

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is null)
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            orderId = (Guid)result;
        }
```
(The `order_items` insert loop below it stays unchanged.)

- [x] **Step 3: Add GetByUserAsync**

Add this method to `OrderRepository`:
```csharp
    /// <summary>Returns a user's orders (newest first) with their line items.</summary>
    public async Task<List<OrderView>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var orders = new List<(Guid Id, string Status, decimal Total, string Currency, DateTime CreatedAt)>();
        await using (var cmd = _db.DataSource.CreateCommand(
            "select id, status, total, currency, created_at from public.orders where user_id = $1 order by created_at desc"))
        {
            cmd.Parameters.AddWithValue(userId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                orders.Add((reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2),
                            reader.GetString(3), reader.GetDateTime(4)));
        }

        var views = new List<OrderView>();
        foreach (var o in orders)
        {
            var items = new List<OrderItemView>();
            await using var icmd = _db.DataSource.CreateCommand(
                "select name, unit_price, quantity from public.order_items where order_id = $1");
            icmd.Parameters.AddWithValue(o.Id);
            await using var ireader = await icmd.ExecuteReaderAsync(ct);
            while (await ireader.ReadAsync(ct))
                items.Add(new OrderItemView(ireader.GetString(0), ireader.GetDecimal(1), ireader.GetInt32(2)));

            views.Add(new OrderView(o.Id, o.Status, o.Total, o.Currency, o.CreatedAt, items));
        }
        return views;
    }
```

- [x] **Step 4: Build**

Run: `cd src/MyMiniCar.Api && dotnet build`
Expected: 0 errors. (The webhook in Program.cs still constructs `PaidOrderInput` without `UserId` — it will fail to compile until Task 2 adds the argument. If so, proceed to Task 2 before building; otherwise this builds clean.)

NOTE: Because adding `UserId` as the first positional arg breaks the webhook's `new PaidOrderInput(...)`, do Task 2 Step 2 in the SAME turn as this task so the project compiles. Commit both together at the end of Task 2.

- [x] **Step 5: (deferred commit — see Task 2)**

---

## Task 2: Api — stamp user_id + /api/orders/mine

**Files:**
- Modify: `src/MyMiniCar.Api/Program.cs`

- [x] **Step 1: Stamp user_id into create-session metadata**

In the `/api/checkout/create-session` endpoint, add an optional `ClaimsPrincipal user` parameter to the handler signature:
```csharp
app.MapPost("/api/checkout/create-session", (CreateCheckoutRequest req, ClaimsPrincipal user) =>
```
Then, in the `Metadata` dictionary, add a `user_id` entry (empty string when anonymous):
```csharp
            ["shipping_amount"] = req.ShippingAmount.ToString(CultureInfo.InvariantCulture),
            ["user_id"] = user.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? user.FindFirstValue("sub") ?? string.Empty
```
(Add the trailing comma to the existing `shipping_amount` line.)

- [x] **Step 2: Read user_id in the webhook**

In the webhook handler, where `PaidOrderInput` is constructed, parse the metadata user id and pass it as the new first argument:
```csharp
    Guid? userId = Guid.TryParse(m.GetValueOrDefault("user_id"), out var uid) ? uid : null;

    var input = new PaidOrderInput(
        UserId: userId,
        StripeSessionId: session.Id,
        Email: session.CustomerDetails?.Email ?? session.CustomerEmail,
        CustomerName: m.GetValueOrDefault("customer_name"),
        CustomerPhone: m.GetValueOrDefault("customer_phone"),
        Subtotal: total - shipping,
        ShippingAmount: shipping,
        Total: total,
        Currency: session.Currency ?? "eur",
        Carrier: "econt",
        ShippingMethod: m.GetValueOrDefault("delivery_mode"),
        ShippingJson: shippingJson,
        Items: items);
```

- [x] **Step 3: Add GET /api/orders/mine**

Near `/api/auth/me`, add:
```csharp
app.MapGet("/api/orders/mine", async (ClaimsPrincipal user, OrderRepository orders) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    if (sub is null || !Guid.TryParse(sub, out var userId))
        return Results.Unauthorized();
    return Results.Ok(await orders.GetByUserAsync(userId));
}).RequireAuthorization();
```

- [x] **Step 4: Build + smoke**

Run:
```bash
cd src/MyMiniCar.Api && dotnet build
```
Expected: 0 errors. Then run the Api and confirm `/api/orders/mine` requires auth:
```bash
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
curl -s -o /dev/null -w "no-auth=%{http_code}\n" http://localhost:5230/api/orders/mine
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: `no-auth=401`.

- [x] **Step 5: Commit (Tasks 1 + 2 together)**

```bash
git add src/MyMiniCar.Api/Models/OrderModels.cs src/MyMiniCar.Api/Data/OrderRepository.cs src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): link orders to user + GET /api/orders/mine

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Web — authed orders service + account page

**Files:**
- Modify: `src/MyMiniCar.Web/Services/CheckoutService.cs`
- Create: `src/MyMiniCar.Web/Services/OrdersService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`
- Create: `src/MyMiniCar.Web/Pages/Account.razor`
- Modify: `src/MyMiniCar.Web/Shared/NavMenu.razor`

- [x] **Step 1: Make CheckoutService attach the bearer token**

Replace `src/MyMiniCar.Web/Services/CheckoutService.cs` constructor + create call so it sends the JWT when present. Change the class to take a `TokenStore`:
```csharp
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
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CreateSessionResult>();
    }

    public async Task<SessionStatus?> GetSessionAsync(string sessionId)
    {
        try { return await _http.GetFromJsonAsync<SessionStatus>($"/api/checkout/session/{sessionId}"); }
        catch (HttpRequestException) { return null; }
    }
}
```
Keep the existing `using System.Net.Http.Json;` at the top and all the record definitions below the class unchanged.

- [x] **Step 2: Create OrdersService**

`src/MyMiniCar.Web/Services/OrdersService.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

/// <summary>Reads the signed-in user's orders from the Api (bearer-authed).</summary>
public sealed class OrdersService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public OrdersService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<List<OrderView>?> GetMineAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/orders/mine");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<List<OrderView>>();
    }
}

public sealed record OrderItemView(string Name, decimal UnitPrice, int Quantity);
public sealed record OrderView(
    Guid Id, string Status, decimal Total, string Currency,
    DateTime CreatedAt, List<OrderItemView> Items);
```

- [x] **Step 3: Wire DI in Program.cs**

In `src/MyMiniCar.Web/Program.cs`, change the `CheckoutService` registration to pass the `TokenStore`, and register `OrdersService`. The `TokenStore` is registered in the auth block — make sure these lines come AFTER `builder.Services.AddScoped<TokenStore>();`. Replace:
```csharp
builder.Services.AddScoped(_ => new CheckoutService(apiBaseUrl));
```
with (and move it below the `AddScoped<TokenStore>()` line):
```csharp
builder.Services.AddScoped(sp => new CheckoutService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped(sp => new OrdersService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
```

- [x] **Step 4: Create the Account page**

`src/MyMiniCar.Web/Pages/Account.razor`:
```razor
@page "/account"
@attribute [Authorize]
@using MyMiniCar.Web.Services
@inject OrdersService Orders

<PageTitle>My orders</PageTitle>
<div class="account-page">
    <h1>My orders</h1>
    @if (_loading)
    {
        <p>Loading…</p>
    }
    else if (_orders is null || _orders.Count == 0)
    {
        <p>No orders yet.</p>
    }
    else
    {
        @foreach (var o in _orders)
        {
            <div class="order-card">
                <div class="order-head">
                    <span class="order-date">@o.CreatedAt.ToLocalTime().ToString("dd MMM yyyy")</span>
                    <span class="order-status">@o.Status</span>
                    <span class="order-total">@o.Total.ToString("0.00") @o.Currency.ToUpperInvariant()</span>
                </div>
                <ul class="order-items">
                    @foreach (var it in o.Items)
                    {
                        <li>@it.Quantity × @it.Name — @it.UnitPrice.ToString("0.00")</li>
                    }
                </ul>
            </div>
        }
    }
</div>

@code {
    private List<OrderView>? _orders;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        _orders = await Orders.GetMineAsync();
        _loading = false;
    }
}
```

- [x] **Step 5: Add "My orders" to NavMenu**

In `src/MyMiniCar.Web/Shared/NavMenu.razor`, inside the existing `<Authorized>` block (before the email span), add:
```razor
                    <a href="account" class="nav-link nav-link-mmc" @onclick="CloseNavMenu">@Language.T("My orders")</a>
```

- [x] **Step 6: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [x] **Step 7: Commit**

```bash
git add src/MyMiniCar.Web/Services/CheckoutService.cs src/MyMiniCar.Web/Services/OrdersService.cs src/MyMiniCar.Web/Program.cs src/MyMiniCar.Web/Pages/Account.razor src/MyMiniCar.Web/Shared/NavMenu.razor
git commit -m "feat(web): account order-history page (authed)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Verify order linking + history

**Files:** none (verification).

- [x] **Step 1: /api/orders/mine works with a minted token**

```bash
cd src/MyMiniCar.Api
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &); sleep 8
# mint a token for a real user id that has orders, or expect [] for a fresh user
TOKEN="<minted-or-real-jwt>"
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5230/api/orders/mine
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: a JSON array (empty `[]` if that user has no orders yet — proves the authed path without needing a purchase).

- [x] **Step 2: Commit plan complete**

Tick all boxes:
```bash
git add docs/superpowers/plans/2026-06-11-supabase-phase1d-account-orders.md
git commit -m "docs: Phase 1D complete"
```

---

## Self-Review (plan-write time)

- **Spec coverage:** order→user linking + order-history page (spec §6.1/§6.2) ✓. Saved designs = Phase 1E.
- **Placeholders:** none — all code complete.
- **Type consistency:** `OrderView`/`OrderItemView` defined identically in Api (`OrderModels.cs`) and Web (`OrdersService.cs`) so JSON round-trips; `PaidOrderInput.UserId` added as first positional arg and supplied in the webhook (Task 2 Step 2) — both projects must compile together (commit Tasks 1+2 together).
- **Guest safety:** `user_id` is nullable; anonymous checkout sends empty `user_id` metadata → `Guid.TryParse` fails → null → guest order preserved.

## Phase 1D Done =
Api builds; `/api/orders/mine` returns 401 anon / array authed; create-session stamps `user_id`; webhook persists it; `/account` lists orders. Full purchase→history needs the Stripe webhook live test (still pending from Phase 1B).

## Next: Phase 1E — Saved designs
`GET/POST /api/designs` (own rows); "Save design" in the Studio; saved-designs list on the account page; load a saved design back into the Studio.
