# Supabase Phase 0 — Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Execution rule (token-safety):** Execute ONE task per turn. Each task ends in a git commit and ticked checkboxes. To resume after interruption: open this file, find the first unchecked box, continue from there. Working tree is always clean between tasks.

**Goal:** Stand up the Supabase Postgres foundation — schema, RLS, signup trigger, seed data — plus the Api-side Supabase data layer wiring, so later phases can persist orders/products/designs.

**Architecture:** API-mediated (Approach A from the design spec). All DB access flows through `MyMiniCar.Api` using the Supabase service-role key. SQL migrations live in `supabase/migrations/`. Phase 0 produces the DB objects + Api connection wiring; it does NOT yet replace `MockProductService` (that is Phase 1).

**Tech Stack:** Supabase (Postgres 15 + Auth + Storage), SQL migrations, ASP.NET minimal API (.NET 7), `supabase-csharp` / `postgrest-csharp`, Npgsql (optional, server-side).

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`

---

## USER ACTIONS (cannot be automated — do these before/at the marked tasks)

These require your Supabase account; Claude cannot do them.

1. Create a Supabase project at https://supabase.com → note **Project URL**, **anon public key**, **service_role key**, and **DB connection string**.
2. Save the project's **JWT secret** (Settings → API → JWT Settings) — needed in Phase 1 for token validation.
3. Apply migrations: either paste each `supabase/migrations/*.sql` into the Supabase SQL editor in order, or use the Supabase CLI (`supabase db push`).
4. Set Api secrets (never commit these):
   ```bash
   cd src/MyMiniCar.Api
   dotnet user-secrets set "Supabase:Url" "https://<ref>.supabase.co"
   dotnet user-secrets set "Supabase:ServiceRoleKey" "<service_role_key>"
   ```

---

## File Structure

- Create: `supabase/migrations/0001_init.sql` — all tables
- Create: `supabase/migrations/0002_rls.sql` — RLS enable + policies
- Create: `supabase/migrations/0003_profiles_trigger.sql` — auto-create profile on signup
- Create: `supabase/seed.sql` — seed `products` from current `MockProductService` data
- Create: `supabase/README.md` — how to apply migrations + set secrets
- Modify: `src/MyMiniCar.Api/MyMiniCar.Api.csproj` — add `supabase-csharp` package
- Create: `src/MyMiniCar.Api/Data/SupabaseClientFactory.cs` — service-role client wiring
- Modify: `src/MyMiniCar.Api/Program.cs` — register Supabase client + `/api/health/db` endpoint
- Create: `src/MyMiniCar.Api/appsettings.json` keys (placeholder, no secrets) — `Supabase:Url` doc only

---

## Task 1: Schema migration (all tables)

**Files:**
- Create: `supabase/migrations/0001_init.sql`

- [x] **Step 1: Write the schema SQL**

```sql
-- 0001_init.sql — MyMiniCar core schema
-- auth.users is Supabase-managed; do not create it.

-- ── identity ───────────────────────────────────────────────
create table if not exists public.profiles (
  id         uuid primary key references auth.users(id) on delete cascade,
  full_name  text,
  phone      text,
  role       text not null default 'customer',
  created_at timestamptz not null default now()
);

-- ── catalog ────────────────────────────────────────────────
create table if not exists public.products (
  id                text primary key,
  name              text not null,
  description       text not null default '',
  price             numeric(10,2) not null,
  category          text not null,
  image_url         text,
  display_model_url text,
  print_model_url   text,
  default_material  text,
  dimensions        text,
  weight_grams      int not null default 250,
  tile_class        text default 'fil-blue',
  is_featured       bool not null default false,
  is_active         bool not null default true,
  sort_order        int not null default 0,
  created_at        timestamptz not null default now(),
  updated_at        timestamptz not null default now()
);

-- ── saved Studio designs ───────────────────────────────────
create table if not exists public.designs (
  id          uuid primary key default gen_random_uuid(),
  user_id     uuid not null references auth.users(id) on delete cascade,
  product_id  text references public.products(id),
  name        text,
  config      jsonb not null,
  preview_url text,
  source      text not null default 'studio',
  created_at  timestamptz not null default now()
);

-- ── orders ─────────────────────────────────────────────────
create table if not exists public.orders (
  id                uuid primary key default gen_random_uuid(),
  user_id           uuid references auth.users(id) on delete set null,
  stripe_session_id text unique not null,
  status            text not null default 'pending',
  email             text,
  customer_name     text,
  customer_phone    text,
  subtotal          numeric(10,2) not null default 0,
  shipping_amount   numeric(10,2) not null default 0,
  total             numeric(10,2) not null default 0,
  currency          text not null default 'eur',
  carrier           text,
  shipping_method   text,
  shipping          jsonb,
  created_at        timestamptz not null default now(),
  paid_at           timestamptz
);

create table if not exists public.order_items (
  id         uuid primary key default gen_random_uuid(),
  order_id   uuid not null references public.orders(id) on delete cascade,
  product_id text references public.products(id) on delete set null,
  name       text not null,
  unit_price numeric(10,2) not null,
  quantity   int not null,
  config     jsonb
);

-- ── shipments (carrier-agnostic) ───────────────────────────
create table if not exists public.shipments (
  id            uuid primary key default gen_random_uuid(),
  order_id      uuid not null unique references public.orders(id) on delete cascade,
  carrier       text not null,
  service_type  text,
  waybill       text,
  tracking_url  text,
  label_pdf_url text,
  status        text default 'created',
  carrier_data  jsonb,
  cost          numeric(10,2),
  created_at    timestamptz not null default now()
);

-- ── AI gen (placeholder, phase 2) ──────────────────────────
create table if not exists public.ai_generations (
  id               uuid primary key default gen_random_uuid(),
  user_id          uuid not null references auth.users(id) on delete cascade,
  input_image_url  text,
  output_model_url text,
  status           text not null default 'queued',
  error            text,
  created_at       timestamptz not null default now()
);

-- ── indexes ────────────────────────────────────────────────
create index if not exists idx_orders_user        on public.orders(user_id);
create index if not exists idx_orders_status       on public.orders(status);
create index if not exists idx_order_items_order   on public.order_items(order_id);
create index if not exists idx_designs_user        on public.designs(user_id);
create index if not exists idx_products_active      on public.products(is_active);
```

- [x] **Step 2: Verify SQL parses**

Run (optional, if Supabase CLI + local Docker available):
`supabase db reset` (applies migrations to a local shadow DB)
Expected: no syntax errors. If no CLI, eyeball-review for balanced parens / valid types.

- [x] **Step 3: Commit**

```bash
git add supabase/migrations/0001_init.sql
git commit -m "feat(db): Supabase core schema migration

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: RLS policies

**Files:**
- Create: `supabase/migrations/0002_rls.sql`

- [x] **Step 1: Write RLS SQL**

```sql
-- 0002_rls.sql — Row-Level Security
-- The Api uses the service_role key, which BYPASSES RLS. These policies
-- are defense-in-depth for any anon/authenticated direct access.

-- helper: is the current user an admin?
create or replace function public.is_admin()
returns boolean
language sql stable
security definer set search_path = public
as $$
  select exists (
    select 1 from public.profiles
    where id = auth.uid() and role = 'admin'
  );
$$;

alter table public.profiles       enable row level security;
alter table public.products       enable row level security;
alter table public.designs        enable row level security;
alter table public.orders         enable row level security;
alter table public.order_items    enable row level security;
alter table public.shipments      enable row level security;
alter table public.ai_generations enable row level security;

-- profiles: read/update own; admin reads all
create policy profiles_select_own on public.profiles
  for select using (id = auth.uid() or public.is_admin());
create policy profiles_update_own on public.profiles
  for update using (id = auth.uid());

-- products: public read active; admin full
create policy products_read_active on public.products
  for select using (is_active or public.is_admin());
create policy products_admin_write on public.products
  for all using (public.is_admin()) with check (public.is_admin());

-- designs: owner full; admin read
create policy designs_owner_all on public.designs
  for all using (user_id = auth.uid()) with check (user_id = auth.uid());
create policy designs_admin_read on public.designs
  for select using (public.is_admin());

-- orders: owner read; admin read/update
create policy orders_owner_read on public.orders
  for select using (user_id = auth.uid() or public.is_admin());
create policy orders_admin_update on public.orders
  for update using (public.is_admin());

-- order_items: read via own order; admin read
create policy order_items_owner_read on public.order_items
  for select using (
    public.is_admin() or exists (
      select 1 from public.orders o
      where o.id = order_id and o.user_id = auth.uid()
    )
  );

-- shipments: read via own order; admin read/update
create policy shipments_owner_read on public.shipments
  for select using (
    public.is_admin() or exists (
      select 1 from public.orders o
      where o.id = order_id and o.user_id = auth.uid()
    )
  );
create policy shipments_admin_update on public.shipments
  for update using (public.is_admin());

-- ai_generations: owner full; admin read
create policy ai_owner_all on public.ai_generations
  for all using (user_id = auth.uid()) with check (user_id = auth.uid());
create policy ai_admin_read on public.ai_generations
  for select using (public.is_admin());
```

- [x] **Step 2: Verify** — eyeball for balanced `$$`, every table has RLS enabled + at least one policy.

- [x] **Step 3: Commit**

```bash
git add supabase/migrations/0002_rls.sql
git commit -m "feat(db): RLS policies + is_admin helper

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Profiles signup trigger

**Files:**
- Create: `supabase/migrations/0003_profiles_trigger.sql`

- [ ] **Step 1: Write trigger SQL**

```sql
-- 0003_profiles_trigger.sql — auto-create a profile row on signup
create or replace function public.handle_new_user()
returns trigger
language plpgsql security definer set search_path = public
as $$
begin
  insert into public.profiles (id, full_name, phone)
  values (
    new.id,
    new.raw_user_meta_data ->> 'full_name',
    new.raw_user_meta_data ->> 'phone'
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();
```

- [ ] **Step 2: Verify** — eyeball: function `security definer`, trigger `after insert on auth.users`.

- [ ] **Step 3: Commit**

```bash
git add supabase/migrations/0003_profiles_trigger.sql
git commit -m "feat(db): auto-create profile on auth signup

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Seed products

**Files:**
- Create: `supabase/seed.sql`

Source data = `src/MyMiniCar.Web/Services/MockProductService.cs` (8 products). `sort_order` follows list order.

- [ ] **Step 1: Write seed SQL**

```sql
-- seed.sql — initial catalog, mirrors MockProductService
insert into public.products
  (id, name, description, price, category, image_url, default_material, dimensions, tile_class, is_featured, weight_grams, sort_order)
values
  ('golf-keychain', 'VW Golf IV Keychain',
   'The hot-hatch icon, shrunk to a pocket-sized keyring tag. Printed to order in your choice of PLA filament — add your name, plate or race number in the Studio.',
   16.90, 'Hatchback', 'images/cars/golf.png', 'Racing Red', '55mm × 30mm × 4mm', 'fil-red', true, 250, 1),
  ('audi-a4-keychain', 'Audi A4 (B6) Keychain',
   'The understated 2000s saloon, faithfully miniaturised. A clean, premium silhouette that prints beautifully in any finish.',
   16.90, 'Saloon', 'images/cars/audi-a4-2000.png', 'Silver Steel', '55mm × 30mm × 4mm', 'fil-silver', true, 250, 2),
  ('passat-keychain', 'VW Passat B5 Keychain',
   'The do-it-all family Volkswagen as a chunky keyring tag. Looks sharp in deep solid colours.',
   16.90, 'Saloon', 'images/cars/vw-passat.png', 'Racing Blue', '55mm × 30mm × 4mm', 'fil-blue', false, 250, 3),
  ('mercedes-w124-keychain', 'Mercedes W124 300CE Keychain',
   'The bulletproof modern classic. A coupé profile with the kind of presence that earns a premium finish.',
   18.90, 'Classic', 'images/cars/mercedes-w124-300ce.png', 'Marble PLA', '55mm × 30mm × 4mm', 'fil-marble', true, 250, 4),
  ('skoda-octavia-keychain', 'Skoda Octavia Keychain',
   'The dependable daily, miniaturised. A crisp three-box shape that prints clean in every filament.',
   16.90, 'Saloon', 'images/cars/skoda-octavia-2005.png', 'Wood Fill', '55mm × 30mm × 4mm', 'fil-wood', false, 250, 5),
  ('led-keyring', 'LED Light-Up Keyring',
   'Add-on hardware: a press-button LED keyring that clips to any MyMiniCar tag and lights up your custom car on demand.',
   12.00, 'Accessories', null, 'Midnight Black', 'Keyring · 28mm', 'fil-black', false, 250, 6),
  ('display-stand', 'Magnetic Display Stand',
   'A little printed plinth so your keychain doubles as a desk or shelf piece when it''s off the keys.',
   9.50, 'Accessories', null, 'Silver Steel', '40mm × 40mm × 18mm', 'fil-silver', false, 250, 7),
  ('hardware-pack', 'Keyring Hardware Pack',
   'Spare split-rings, lobster clips and ball-chains so you can rig your keychains exactly how you want.',
   5.00, 'Accessories', null, 'Midnight Black', 'Mixed hardware ×10', 'fil-black', false, 250, 8)
on conflict (id) do update set
  name = excluded.name, description = excluded.description, price = excluded.price,
  category = excluded.category, image_url = excluded.image_url,
  default_material = excluded.default_material, dimensions = excluded.dimensions,
  tile_class = excluded.tile_class, is_featured = excluded.is_featured,
  weight_grams = excluded.weight_grams, sort_order = excluded.sort_order,
  updated_at = now();
```

- [ ] **Step 2: Verify** — count = 8 rows, prices/ids match `MockProductService.cs`, note `''` escaping in display-stand description.

- [ ] **Step 3: Commit**

```bash
git add supabase/seed.sql
git commit -m "feat(db): seed products from MockProductService

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Api Supabase client wiring + health endpoint

**Files:**
- Modify: `src/MyMiniCar.Api/MyMiniCar.Api.csproj`
- Create: `src/MyMiniCar.Api/Data/SupabaseClientFactory.cs`
- Modify: `src/MyMiniCar.Api/Program.cs`
- Create: `supabase/README.md`

- [ ] **Step 1: Add the package**

Run:
```bash
cd src/MyMiniCar.Api
dotnet add package Supabase
```
Expected: `Supabase` (the `supabase-csharp` meta-package) added to `MyMiniCar.Api.csproj`.

- [ ] **Step 2: Create the client factory**

Create `src/MyMiniCar.Api/Data/SupabaseClientFactory.cs`:
```csharp
using Supabase;

namespace MyMiniCar.Api.Data;

/// <summary>
/// Builds a single service-role Supabase client for server-side DB access.
/// Service-role key bypasses RLS — never expose it to the browser.
/// </summary>
public sealed class SupabaseClientFactory
{
    private readonly Client _client;

    public SupabaseClientFactory(IConfiguration config)
    {
        var url = config["Supabase:Url"]
            ?? throw new InvalidOperationException(
                "Supabase:Url not configured. Run: dotnet user-secrets set \"Supabase:Url\" \"https://<ref>.supabase.co\"");
        var key = config["Supabase:ServiceRoleKey"]
            ?? throw new InvalidOperationException(
                "Supabase:ServiceRoleKey not configured. Run: dotnet user-secrets set \"Supabase:ServiceRoleKey\" \"<service_role_key>\"");

        _client = new Client(url, key, new SupabaseOptions
        {
            AutoConnectRealtime = false
        });
    }

    public Client Client => _client;

    public Task InitializeAsync() => _client.InitializeAsync();
}
```

- [ ] **Step 3: Register it + add health endpoint in `Program.cs`**

In `src/MyMiniCar.Api/Program.cs`, add the using near the top (after the existing `using MyMiniCar.Api;`):
```csharp
using MyMiniCar.Api.Data;
```
Register the singleton with the other service registrations (after the `AddHttpClient<EcontService>` block, before `var app = builder.Build();`):
```csharp
builder.Services.AddSingleton<SupabaseClientFactory>();
```
Add a health endpoint (just before `app.MapGet("/", ...)`):
```csharp
// DB connectivity probe. Returns 200 if the service-role client initializes.
app.MapGet("/api/health/db", async (SupabaseClientFactory factory) =>
{
    try
    {
        await factory.InitializeAsync();
        return Results.Ok(new { db = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"DB init failed: {ex.Message}");
    }
});
```

- [ ] **Step 4: Build**

Run:
```bash
cd src/MyMiniCar.Api
dotnet build
```
Expected: Build succeeded, 0 errors. (Endpoint returns 500 until USER ACTION secrets are set — that's expected; build correctness is what we verify here.)

- [ ] **Step 5: Write `supabase/README.md`**

```markdown
# Supabase

## Apply migrations
Paste each file in `migrations/` into the Supabase SQL editor in numeric order,
then run `seed.sql`. Or with the Supabase CLI: `supabase db push && psql < seed.sql`.

## Api secrets (never commit)
```
cd src/MyMiniCar.Api
dotnet user-secrets set "Supabase:Url" "https://<ref>.supabase.co"
dotnet user-secrets set "Supabase:ServiceRoleKey" "<service_role_key>"
```

## Verify
Run the Api, then GET /api/health/db → {"db":"ok"}.
```

- [ ] **Step 6: Commit**

```bash
git add src/MyMiniCar.Api/MyMiniCar.Api.csproj src/MyMiniCar.Api/Data/SupabaseClientFactory.cs src/MyMiniCar.Api/Program.cs supabase/README.md
git commit -m "feat(api): Supabase service-role client + db health endpoint

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review (done at plan-write time)

- **Spec coverage:** Phase 0 = schema ✓ (Task 1), RLS ✓ (Task 2), signup trigger ✓ (Task 3), seed products ✓ (Task 4), Api data-layer wiring ✓ (Task 5). Auth middleware, Stripe webhook, ApiProductService = Phase 1 (separate plan). AI gen = Phase 4.
- **Placeholders:** none — all SQL/C# is complete.
- **Type consistency:** `SupabaseClientFactory` defined Task 5 Step 2, used Step 3; `is_admin()` defined Task 2 used across policies.

## Phase 0 Done = 
All 5 task commits landed; `dotnet build` green; (after USER ACTIONS) `GET /api/health/db` → `{"db":"ok"}` and `select count(*) from products` = 8.
```
Then write the Phase 1 plan.
```
