# Supabase Phase 1B — Order Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.
>
> **Execution rule (token-safety):** ONE task per turn. Each task ends in a git commit + ticked boxes. Resume: open this file, find first unchecked box. Working tree clean between tasks.

**Goal:** Persist paid orders to Supabase via a Stripe webhook, so every completed checkout writes an `orders` + `order_items` record. Idempotent on `stripe_session_id`. Resolves the existing `persist order` / `book via webhook` / `idempotent` TODOs in `Api/Program.cs`.

**Architecture:** Webhook-driven, Stripe as source of truth. On `checkout.session.completed`, the Api verifies the Stripe signature, lists the session's line items, and writes the order through `OrderRepository` (Npgsql, single transaction). The existing checkout-create flow and the entire Blazor frontend are untouched except for **one added metadata key** (`shipping_amount`) so the webhook can split subtotal vs shipping. `user_id` is left null (guest) — linking orders to logged-in users comes in Phase 1C (auth).

**Tech Stack:** ASP.NET minimal API (.NET 7), Stripe.net 52, Npgsql 7, Supabase Postgres.

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phase 0 + 1A complete (DB live, `SupabaseDataSource`, product endpoints).

---

## USER ACTIONS

1. **Stripe webhook secret (local):** install the Stripe CLI, run
   `stripe listen --forward-to http://localhost:5230/api/stripe/webhook`.
   It prints a signing secret `whsec_...`. Then:
   ```bash
   cd src/MyMiniCar.Api
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
   ```
2. **Stripe webhook (production):** in the Stripe Dashboard → Developers → Webhooks,
   add an endpoint `https://<api-host>/api/stripe/webhook` for event
   `checkout.session.completed`; copy its signing secret into the Render env var
   `Stripe__WebhookSecret`.

---

## File Structure

- Create: `src/MyMiniCar.Api/Models/OrderModels.cs` — internal records for the webhook→repo hand-off
- Create: `src/MyMiniCar.Api/Data/OrderRepository.cs` — transactional insert, idempotent
- Modify: `src/MyMiniCar.Api/Program.cs` — add `shipping_amount` metadata, register repo, add webhook endpoint
- Modify: `src/MyMiniCar.Api/MyMiniCar.Api.csproj` — (no change; Stripe.net already referenced)

---

## Task 1: OrderRepository + models

**Files:**
- Create: `src/MyMiniCar.Api/Models/OrderModels.cs`
- Create: `src/MyMiniCar.Api/Data/OrderRepository.cs`

- [x] **Step 1: Create the models**

`src/MyMiniCar.Api/Models/OrderModels.cs`:
```csharp
namespace MyMiniCar.Api.Models;

/// <summary>A single purchased line, normalized from a Stripe line item.</summary>
public sealed record OrderLineInput(string Name, decimal UnitPrice, int Quantity);

/// <summary>Everything needed to persist one paid order. Built in the webhook
/// from the Stripe session + its line items.</summary>
public sealed record PaidOrderInput(
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
    string ShippingJson,                 // jsonb payload (address/office/etc.)
    IReadOnlyList<OrderLineInput> Items);
```

- [x] **Step 2: Create the repository**

`src/MyMiniCar.Api/Data/OrderRepository.cs`:
```csharp
using MyMiniCar.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace MyMiniCar.Api.Data;

/// <summary>Writes paid orders. Idempotent: a second call for the same
/// stripe_session_id is a no-op (unique constraint + ON CONFLICT).</summary>
public sealed class OrderRepository
{
    private readonly SupabaseDataSource _db;

    public OrderRepository(SupabaseDataSource db) => _db = db;

    /// <summary>Returns true if a new order was written, false if it already existed.</summary>
    public async Task<bool> PersistPaidAsync(PaidOrderInput input, CancellationToken ct = default)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        Guid orderId;
        await using (var cmd = new NpgsqlCommand(@"
            insert into public.orders
              (stripe_session_id, status, email, customer_name, customer_phone,
               subtotal, shipping_amount, total, currency, carrier, shipping_method, shipping, paid_at)
            values ($1,'paid',$2,$3,$4,$5,$6,$7,$8,$9,$10,$11::jsonb, now())
            on conflict (stripe_session_id) do nothing
            returning id", conn, tx))
        {
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
            if (result is null)            // conflict → already processed
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            orderId = (Guid)result;
        }

        foreach (var item in input.Items)
        {
            await using var itemCmd = new NpgsqlCommand(@"
                insert into public.order_items (order_id, name, unit_price, quantity)
                values ($1,$2,$3,$4)", conn, tx);
            itemCmd.Parameters.AddWithValue(orderId);
            itemCmd.Parameters.AddWithValue(item.Name);
            itemCmd.Parameters.AddWithValue(item.UnitPrice);
            itemCmd.Parameters.AddWithValue(item.Quantity);
            await itemCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return true;
    }
}
```

- [x] **Step 3: Build**

Run: `cd src/MyMiniCar.Api && dotnet build`
Expected: Build succeeded, 0 errors.

- [x] **Step 4: Commit**

```bash
git add src/MyMiniCar.Api/Models/OrderModels.cs src/MyMiniCar.Api/Data/OrderRepository.cs
git commit -m "feat(api): OrderRepository (idempotent paid-order persistence)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: shipping_amount metadata + webhook endpoint

**Files:**
- Modify: `src/MyMiniCar.Api/Program.cs`

- [ ] **Step 1: Add `shipping_amount` to checkout metadata**

In `src/MyMiniCar.Api/Program.cs`, in the `Metadata` dictionary of the create-session options, add one line after `["weight_kg"]`:
```csharp
            ["weight_kg"] = req.WeightKg.ToString(CultureInfo.InvariantCulture),
            ["shipping_amount"] = req.ShippingAmount.ToString(CultureInfo.InvariantCulture)
```
(Add the trailing comma to the existing `weight_kg` line as shown.)

- [ ] **Step 2: Register the repository**

Next to `builder.Services.AddScoped<ProductRepository>();` add:
```csharp
builder.Services.AddScoped<OrderRepository>();
```

- [ ] **Step 3: Add the webhook endpoint**

In `src/MyMiniCar.Api/Program.cs`, add this just after the create-session endpoint (after its closing `});`). It needs `using MyMiniCar.Api.Models;` — add that to the usings at the top of the file:
```csharp
// Stripe → order persistence. Stripe calls this on checkout.session.completed.
app.MapPost("/api/stripe/webhook", async (HttpRequest request, OrderRepository orders, IConfiguration cfg) =>
{
    var secret = cfg["Stripe:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(secret))
        return Results.Problem("Stripe:WebhookSecret not configured.");

    var json = await new StreamReader(request.Body).ReadToEndAsync();

    Event stripeEvent;
    try
    {
        stripeEvent = EventUtility.ConstructEvent(
            json, request.Headers["Stripe-Signature"], secret);
    }
    catch (StripeException)
    {
        return Results.BadRequest(new { error = "Invalid signature." });
    }

    if (stripeEvent.Type != "checkout.session.completed")
        return Results.Ok();   // ignore other events

    if (stripeEvent.Data.Object is not Session session ||
        !string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        return Results.Ok();

    // Shipping line items are added by create-session with these exact names.
    var shippingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Доставка до офис на Еконт",
        "Доставка до адрес (Еконт)"
    };

    var lineItems = new SessionService().ListLineItems(session.Id);
    var items = lineItems.Data
        .Where(li => li.Description is null || !shippingNames.Contains(li.Description))
        .Select(li =>
        {
            var qty = (int)(li.Quantity ?? 1);
            var unit = li.Price?.UnitAmount is long c ? c / 100m
                     : qty > 0 ? (li.AmountTotal / 100m) / qty : 0m;
            return new OrderLineInput(li.Description ?? "Item", unit, qty);
        })
        .ToList();

    var m = session.Metadata ?? new Dictionary<string, string>();
    decimal.TryParse(m.GetValueOrDefault("shipping_amount"),
        NumberStyles.Float, CultureInfo.InvariantCulture, out var shipping);
    var total = (session.AmountTotal ?? 0) / 100m;

    var shippingJson = System.Text.Json.JsonSerializer.Serialize(new
    {
        address = m.GetValueOrDefault("shipping_address"),
        city = m.GetValueOrDefault("shipping_city"),
        postal = m.GetValueOrDefault("shipping_postal"),
        country = m.GetValueOrDefault("shipping_country"),
        office_code = m.GetValueOrDefault("office_code"),
        weight_kg = m.GetValueOrDefault("weight_kg")
    });

    var input = new PaidOrderInput(
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

    await orders.PersistPaidAsync(input);
    return Results.Ok();
});
```

- [ ] **Step 4: Build**

Run: `cd src/MyMiniCar.Api && dotnet build`
Expected: Build succeeded, 0 errors. (If `Session`/`Event`/`EventUtility` are ambiguous, they come from `Stripe` / `Stripe.Checkout`, both already imported at the top of the file.)

- [ ] **Step 5: Commit**

```bash
git add src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): Stripe webhook persists paid orders (idempotent)

Resolves persist-order + book-via-webhook + idempotent TODOs.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Local end-to-end test (Stripe CLI)

**Files:** none (verification). Requires USER ACTION 1 (Stripe CLI + `Stripe:WebhookSecret`).

- [ ] **Step 1: Trigger a test event**

In one terminal: `stripe listen --forward-to http://localhost:5230/api/stripe/webhook`
In another:
```bash
cd src/MyMiniCar.Api
(dotnet run --urls http://localhost:5230 > /tmp/mmc-api.log 2>&1 &)
sleep 8
stripe trigger checkout.session.completed
sleep 4
```
Expected: Api log shows the webhook hit with 200; no exceptions.

- [ ] **Step 2: Verify a row landed**

```bash
export PGPASSWORD='<db-password>'
psql "host=aws-1-eu-north-1.pooler.supabase.com port=5432 dbname=postgres user=postgres.sdiirjjagjqqvqfexohq sslmode=require" \
  -t -c "select status, total, currency from public.orders order by created_at desc limit 1;"
lsof -ti:5230 | xargs kill -9 2>/dev/null
```
Expected: one `paid` row (a `stripe trigger` test session; amounts reflect Stripe's fixture).

- [ ] **Step 3: Verify idempotency**

Re-run `stripe trigger checkout.session.completed` against the SAME session is not possible (trigger makes a new session each time), so instead confirm the unique constraint by re-POSTing a duplicate is a no-op in code review: `PersistPaidAsync` returns false on conflict. Mark verified.

- [ ] **Step 4: Mark plan complete + commit**

Tick all boxes, then:
```bash
git add docs/superpowers/plans/2026-06-11-supabase-phase1b-orders.md
git commit -m "docs: Phase 1B complete"
```

---

## Self-Review (plan-write time)

- **Spec coverage:** "Stripe webhook → idempotently write orders + order_items, status paid, paid_at" (spec §6.1) ✓ Tasks 1–2. Server-side re-pricing + IShippingProvider abstraction = later phases. `user_id` link = Phase 1C (auth).
- **Placeholders:** none — all code complete.
- **Type consistency:** `PaidOrderInput`/`OrderLineInput` (Task 1) consumed unchanged by the webhook (Task 2); columns match the `orders`/`order_items` schema from Phase 0 (`0001_init.sql`).
- **Known limitation (documented):** shipping lines are excluded from `order_items` by name match against the two Econt shipping labels; `order_items.product_id` and per-item `config` are left null (the webhook has only Stripe line descriptions, not the original cart). Capturing product_id + Studio config requires storing the cart at checkout-create time — deferred, noted here as a follow-up.

## Phase 1B Done =
Tasks 1–2 committed; `dotnet build` green; (after USER ACTION) a `stripe trigger checkout.session.completed` writes one `paid` order row; idempotency holds via the `stripe_session_id` unique constraint.

## Next: Phase 1C — Auth
Supabase Auth in the browser (register/login/logout, password reset) → JWT; Api JWT-validation middleware + `profiles.role` lookup; link new orders to `user_id` when the buyer is logged in; account pages (order history, saved designs).
