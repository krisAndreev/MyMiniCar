# Supabase Phase 2C — Admin Analytics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Checkbox steps.
> **Execution rule (token-safety):** ONE task per turn, commit each. Resume from first unchecked box. Working tree clean between tasks.

**Goal:** An admin sees a dashboard with key numbers — total revenue, order count, average order value, a status breakdown, top products, and a short revenue-by-day trend.

**Architecture:** A read-only `AnalyticsRepository` runs aggregate SQL over `orders` + `order_items`. One admin-only endpoint returns a single summary object; an `/admin/analytics` page renders cards + small tables. Revenue counts only fulfilled-ish statuses (`paid`, `shipped`, `delivered`). No new infra or secrets.

**Tech Stack:** ASP.NET minimal API (.NET 7), Npgsql, Blazor WASM (Authorization).

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phases 0–2B complete (admin gate, orders).

---

## File Structure

- Create: `src/MyMiniCar.Api/Models/AnalyticsModels.cs`
- Create: `src/MyMiniCar.Api/Data/AnalyticsRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs` — register repo + `GET /api/admin/analytics`
- Create: `src/MyMiniCar.Web/Services/AdminAnalyticsService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs` — register service
- Create: `src/MyMiniCar.Web/Pages/Admin/Analytics.razor`
- Modify: `src/MyMiniCar.Web/Pages/Admin/Products.razor` and `Orders.razor` — add Analytics to the admin nav row

---

## Task 1: Api — analytics repository + endpoint

**Files:**
- Create: `src/MyMiniCar.Api/Models/AnalyticsModels.cs`
- Create: `src/MyMiniCar.Api/Data/AnalyticsRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs`

- [x] **Step 1: Create the models**

`src/MyMiniCar.Api/Models/AnalyticsModels.cs`:
```csharp
namespace MyMiniCar.Api.Models;

public sealed record StatusCount(string Status, int Count);
public sealed record TopProduct(string Name, int Quantity, decimal Revenue);
public sealed record RevenuePoint(DateTime Day, decimal Revenue);

public sealed record AnalyticsSummary(
    decimal TotalRevenue,
    int OrderCount,
    int PaidOrderCount,
    decimal AverageOrderValue,
    IReadOnlyList<StatusCount> StatusCounts,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<RevenuePoint> RevenueByDay);
```

- [x] **Step 2: Create the repository**

`src/MyMiniCar.Api/Data/AnalyticsRepository.cs`:
```csharp
using MyMiniCar.Api.Models;

namespace MyMiniCar.Api.Data;

/// <summary>Read-only aggregates over orders + order_items for the admin dashboard.
/// "Revenue" counts only paid/shipped/delivered orders.</summary>
public sealed class AnalyticsRepository
{
    private const string RevenueStatuses = "('paid','shipped','delivered')";
    private readonly SupabaseDataSource _db;

    public AnalyticsRepository(SupabaseDataSource db) => _db = db;

    public async Task<AnalyticsSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        decimal totalRevenue = 0; int orderCount = 0; int paidCount = 0;
        await using (var cmd = _db.DataSource.CreateCommand($@"
            select
              coalesce(sum(total) filter (where status in {RevenueStatuses}), 0),
              count(*),
              count(*) filter (where status in {RevenueStatuses})
            from public.orders"))
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                totalRevenue = r.GetDecimal(0);
                orderCount = (int)r.GetInt64(1);
                paidCount = (int)r.GetInt64(2);
            }
        }
        var aov = paidCount > 0 ? Math.Round(totalRevenue / paidCount, 2) : 0m;

        var statusCounts = new List<StatusCount>();
        await using (var cmd = _db.DataSource.CreateCommand(
            "select status, count(*) from public.orders group by status order by count(*) desc"))
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                statusCounts.Add(new StatusCount(r.GetString(0), (int)r.GetInt64(1)));
        }

        var topProducts = new List<TopProduct>();
        await using (var cmd = _db.DataSource.CreateCommand($@"
            select oi.name, sum(oi.quantity)::int, sum(oi.unit_price * oi.quantity)
            from public.order_items oi
            join public.orders o on o.id = oi.order_id
            where o.status in {RevenueStatuses}
            group by oi.name
            order by sum(oi.quantity) desc
            limit 5"))
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                topProducts.Add(new TopProduct(r.GetString(0), r.GetInt32(1), r.GetDecimal(2)));
        }

        var revenueByDay = new List<RevenuePoint>();
        await using (var cmd = _db.DataSource.CreateCommand($@"
            select date_trunc('day', created_at)::date, coalesce(sum(total), 0)
            from public.orders
            where status in {RevenueStatuses} and created_at >= now() - interval '14 days'
            group by 1 order by 1"))
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                revenueByDay.Add(new RevenuePoint(r.GetFieldValue<DateTime>(0), r.GetDecimal(1)));
        }

        return new AnalyticsSummary(totalRevenue, orderCount, paidCount, aov,
                                    statusCounts, topProducts, revenueByDay);
    }
}
```

- [x] **Step 3: Register repo + endpoint in Program.cs**

Next to the other repo registrations:
```csharp
builder.Services.AddScoped<AnalyticsRepository>();
```
Next to the admin endpoints:
```csharp
app.MapGet("/api/admin/analytics", async (ClaimsPrincipal user, ProfileRepository profiles, AnalyticsRepository analytics) =>
{
    var guard = await RequireAdmin(user, profiles);
    if (guard is not null) return guard;
    return Results.Ok(await analytics.GetSummaryAsync());
}).RequireAuthorization();
```

- [x] **Step 4: Build + smoke**

Run:
```bash
cd src/MyMiniCar.Api && dotnet build
lsof -ti:5230 | xargs kill -9 2>/dev/null; (dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &)
for i in $(seq 1 30); do grep -q "Now listening" /tmp/mmc-api.log 2>/dev/null && break; sleep 1; done; sleep 2
curl -s -o /dev/null -w "analytics-anon=%{http_code}\n" http://localhost:5230/api/admin/analytics
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: 0 errors; `analytics-anon=401`.

- [x] **Step 5: Commit**

```bash
git add src/MyMiniCar.Api/Models/AnalyticsModels.cs src/MyMiniCar.Api/Data/AnalyticsRepository.cs src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): admin analytics summary endpoint

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Web — analytics dashboard page

**Files:**
- Create: `src/MyMiniCar.Web/Services/AdminAnalyticsService.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`
- Create: `src/MyMiniCar.Web/Pages/Admin/Analytics.razor`
- Modify: `src/MyMiniCar.Web/Pages/Admin/Products.razor`, `src/MyMiniCar.Web/Pages/Admin/Orders.razor`

- [x] **Step 1: Create the service**

`src/MyMiniCar.Web/Services/AdminAnalyticsService.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyMiniCar.Web.Services;

public sealed class AdminAnalyticsService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public AdminAnalyticsService(string apiBaseUrl, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        _tokens = tokens;
    }

    public async Task<AnalyticsSummary?> GetAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return null;
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/analytics");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AnalyticsSummary>() : null;
    }
}

public sealed record StatusCount(string Status, int Count);
public sealed record TopProduct(string Name, int Quantity, decimal Revenue);
public sealed record RevenuePoint(DateTime Day, decimal Revenue);
public sealed record AnalyticsSummary(
    decimal TotalRevenue, int OrderCount, int PaidOrderCount, decimal AverageOrderValue,
    List<StatusCount> StatusCounts, List<TopProduct> TopProducts, List<RevenuePoint> RevenueByDay);
```

- [x] **Step 2: Register the service**

In `src/MyMiniCar.Web/Program.cs`, next to `AdminOrdersService`:
```csharp
builder.Services.AddScoped(sp => new AdminAnalyticsService(apiBaseUrl, sp.GetRequiredService<TokenStore>()));
```

- [x] **Step 3: Create the analytics page**

`src/MyMiniCar.Web/Pages/Admin/Analytics.razor`:
```razor
@page "/admin/analytics"
@attribute [Authorize(Roles = "admin")]
@using MyMiniCar.Web.Services
@inject AdminAnalyticsService Admin

<PageTitle>Admin · Analytics</PageTitle>
<div class="admin-page container py-5">
    <div class="d-flex gap-3 mb-3">
        <a href="admin/products" class="nav-link-mmc">Products</a>
        <a href="admin/orders" class="nav-link-mmc">Orders</a>
        <a href="admin/analytics" class="nav-link-mmc">Analytics</a>
    </div>
    <h1>Analytics</h1>

    @if (_data is null)
    {
        <p>Loading…</p>
    }
    else
    {
        <div class="stat-cards">
            <div class="stat-card"><span class="stat-label">Revenue</span><span class="stat-value">@_data.TotalRevenue.ToString("0.00")</span></div>
            <div class="stat-card"><span class="stat-label">Orders</span><span class="stat-value">@_data.OrderCount</span></div>
            <div class="stat-card"><span class="stat-label">Paid orders</span><span class="stat-value">@_data.PaidOrderCount</span></div>
            <div class="stat-card"><span class="stat-label">Avg order</span><span class="stat-value">@_data.AverageOrderValue.ToString("0.00")</span></div>
        </div>

        <h2 class="mt-4">By status</h2>
        <ul>@foreach (var s in _data.StatusCounts) { <li>@s.Status — @s.Count</li> }</ul>

        <h2 class="mt-4">Top products</h2>
        @if (_data.TopProducts.Count == 0) { <p>No sales yet.</p> }
        else
        {
            <table class="admin-table">
                <thead><tr><th>Product</th><th>Qty</th><th>Revenue</th></tr></thead>
                <tbody>
                    @foreach (var p in _data.TopProducts)
                    {
                        <tr><td>@p.Name</td><td>@p.Quantity</td><td>@p.Revenue.ToString("0.00")</td></tr>
                    }
                </tbody>
            </table>
        }

        <h2 class="mt-4">Revenue (last 14 days)</h2>
        @if (_data.RevenueByDay.Count == 0) { <p>No revenue yet.</p> }
        else
        {
            <ul>@foreach (var d in _data.RevenueByDay) { <li>@d.Day.ToString("dd MMM") — @d.Revenue.ToString("0.00")</li> }</ul>
        }
    }
</div>

@code {
    private AnalyticsSummary? _data;
    protected override async Task OnInitializedAsync() => _data = await Admin.GetAsync();
}
```

- [x] **Step 4: Add Analytics to the admin nav rows**

In BOTH `src/MyMiniCar.Web/Pages/Admin/Products.razor` and `src/MyMiniCar.Web/Pages/Admin/Orders.razor`, in the `<div class="d-flex gap-3 mb-3">` nav row, add after the Orders link:
```razor
        <a href="admin/analytics" class="nav-link-mmc">Analytics</a>
```

- [x] **Step 5: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: 0 errors.

- [x] **Step 6: Commit**

```bash
git add src/MyMiniCar.Web/Services/AdminAnalyticsService.cs src/MyMiniCar.Web/Program.cs src/MyMiniCar.Web/Pages/Admin/Analytics.razor src/MyMiniCar.Web/Pages/Admin/Products.razor src/MyMiniCar.Web/Pages/Admin/Orders.razor
git commit -m "feat(web): admin analytics dashboard

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Live verify

**Files:** none.

- [x] **Step 1: Admin fetches analytics**

```bash
cd src/MyMiniCar.Api
lsof -ti:5230 | xargs kill -9 2>/dev/null; (dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &)
for i in $(seq 1 30); do grep -q "Now listening" /tmp/mmc-api.log 2>/dev/null && break; sleep 1; done; sleep 2
URL="https://sdiirjjagjqqvqfexohq.supabase.co"; ANON="<anon-key>"
AT=$(curl -s -X POST "$URL/auth/v1/token?grant_type=password" -H "apikey: $ANON" -H "Content-Type: application/json" -d '{"email":"mymincar.admin@gmail.com","password":"MmcAdmin2026!"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")
curl -s -H "Authorization: Bearer $AT" http://localhost:5230/api/admin/analytics | python3 -m json.tool
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: a JSON summary with `totalRevenue`, `orderCount`, `statusCounts`, `topProducts` (reflecting the existing test order — 1 order, revenue 30, top product the Stripe fixture item).

- [x] **Step 2: Commit plan complete**

Tick all boxes, commit the plan file.

---

## Self-Review (plan-write time)

- **Spec coverage:** analytics (revenue, top products, order counts, AOV) — spec §6.2 admin analytics ✓. Storage uploads = Phase 2D.
- **Placeholders:** none. `<anon-key>` is run-time public key.
- **Type consistency:** `AnalyticsSummary` + nested records defined identically in Api (`AnalyticsModels.cs`) and Web (`AdminAnalyticsService.cs`); revenue statuses constant shared as SQL literal; `RequireAdmin` reused.
- **Numeric care:** `count(*)` is `bigint` → read via `GetInt64` then cast to int; AOV guarded against divide-by-zero.

## Phase 2C Done =
Both build green; `/api/admin/analytics` 401 anon / 200 admin with a populated summary; `/admin/analytics` renders cards + tables; admin nav links across products/orders/analytics.

## Next: Phase 2D — Storage uploads
Supabase Storage buckets (product-images, models) + admin file upload. Needs either the service_role key (Api-proxied upload) or storage RLS policies (browser-direct upload with the admin JWT).
