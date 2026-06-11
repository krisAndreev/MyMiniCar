# Supabase DB Integration — Design Spec

**Date:** 2026-06-11
**Status:** In review (schema approved through carrier-agnostic shipments; RLS / functionalities / phasing drafted below, pending final approval)
**Author:** Kris + Claude (brainstorming)

---

## 1. Context & Goal

MyMiniCar is a Blazor WebAssembly store selling 3D-printed car keychains, with a custom "Studio" configurator (size / material / template / engraving text) and Stripe checkout + Econt shipping.

**Current state:**
- **`MyMiniCar.Web`** — Blazor WASM, deployed as a Render **static site**. Holds no secrets. Products are hard-coded in `MockProductService`. Cart + language in singletons. No persistence of anything.
- **`MyMiniCar.Api`** — ASP.NET minimal API (Render **web service**). Holds Stripe + Econt secrets. Creates Stripe Checkout sessions, calls Econt for shipping quotes/labels. **No database — orders are never persisted.** Code already carries TODOs: `persist order + shipment number`, `book label via Stripe webhook`, `make idempotent`, `re-price shipping server-side`.

**Goal:** Add a Supabase (Postgres + Auth + Storage) database to make this a real go-live store: persist products + orders, customer accounts with order history + saved designs, an admin console (product CRUD + order management + analytics), and a placeholder for future AI image→3D generation.

## 2. Decisions (locked)

| Topic | Decision |
|---|---|
| **Driver** | Real go-live store → production-grade, multi-phase. |
| **Accounts** | **Full accounts** (register/login, order history, saved designs, AI later) **+ guest checkout still allowed**. Orders have nullable `user_id`. |
| **AI 3D-gen** | **Deferred to a later phase.** Phase 1 ships only a placeholder `ai_generations` table. Build-vs-buy decided later (leaning "buy" — external API like Meshy/Tripo/Luma). |
| **Admin console** | **Full ops**: product CRUD (add/edit/disable, price, image, model files), view orders + update status, analytics (revenue, top products, order counts, AOV). |
| **Architecture** | **API-mediated (Approach A).** All DB access flows through `MyMiniCar.Api` using the Supabase **service-role key** (server-only). Browser logs in via Supabase Auth (anon key), receives a JWT, and calls our Api with `Authorization: Bearer <jwt>`. |
| **Model formats** | Store format-agnostic URLs. **GLB/GLTF** for the browser viewer (already used by `CarModelViewer.razor` + `car-model-viewer.js`); **STL/3MF** for print/fulfillment. Both optional columns. |
| **Shipping carriers** | **Carrier-agnostic from day one.** Econt now; **Speedy** and **BoxNow/EasyBox (APM)** planned. Schema + an `IShippingProvider` abstraction must allow adding carriers with zero migration. |

## 3. Architecture & Auth Flow

```
Browser (Blazor WASM, static site)
  │  1. register/login → Supabase Auth (anon key) → returns JWT
  │  2. all data calls carry JWT → Authorization: Bearer <jwt>
  ▼
ASP.NET Api (Render web service, always-on)
  │  - validates Supabase JWT (verify signature via Supabase JWKS / project JWT secret)
  │  - reads role from `profiles` (admin endpoints require role='admin')
  │  - uses Supabase service-role key for trusted DB writes
  │  - holds Stripe + Econt (+ future Speedy/BoxNow) secrets
  ▼
Supabase Postgres + Storage
```

- **Identity:** Supabase Auth, email+password to start (OAuth optional later). Browser holds the JWT; sends it on every Api call.
- **Authorization:** Api validates the JWT signature, then looks up `profiles.role`. Admin-only endpoints reject non-admins. RLS stays ON as defense-in-depth even though the Api uses the service-role key for trusted writes.
- **Order truth:** A **Stripe webhook** (`checkout.session.completed`) → Api → writes the `orders` row. This replaces the fragile confirmation-page polling and resolves the existing `persist order` / `webhook` / `idempotent` TODOs. `stripe_session_id` is the idempotency key.
- **Storage buckets:** `product-images`, `models` (glb / stl), `ai-uploads` (phase 2).

## 4. Schema (Postgres / Supabase)

`auth.users` is Supabase-managed — do not create it. All app tables below.

```sql
-- ── identity ───────────────────────────────────────────────
profiles (
  id           uuid pk references auth.users(id) on delete cascade,
  full_name    text,
  phone        text,
  role         text not null default 'customer',  -- 'customer' | 'admin'
  created_at   timestamptz not null default now()
)
-- row auto-created on signup via trigger on auth.users

-- ── catalog ────────────────────────────────────────────────
products (
  id                 text pk,              -- keep slug ids ('golf-keychain')
  name               text not null,
  description        text not null default '',
  price              numeric(10,2) not null,
  category           text not null,
  image_url          text,                 -- product-images bucket
  display_model_url  text,                 -- glb for browser viewer
  print_model_url    text,                 -- stl/3mf for fulfillment
  default_material   text,
  dimensions         text,
  weight_grams       int not null default 250,
  tile_class         text default 'fil-blue',
  is_featured        bool not null default false,
  is_active          bool not null default true,   -- soft-delete / hide
  sort_order         int not null default 0,
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now()
)

-- ── saved Studio designs (logged-in users) ─────────────────
designs (
  id           uuid pk default gen_random_uuid(),
  user_id      uuid not null references auth.users(id) on delete cascade,
  product_id   text references products(id),   -- base car, nullable for AI-origin
  name         text,                           -- user label
  config       jsonb not null,                 -- StudioConfig: size/material/template/line1/line2/color
  preview_url  text,                           -- optional rendered thumb
  source       text not null default 'studio', -- 'studio' | 'ai'
  created_at   timestamptz not null default now()
)

-- ── orders (Stripe webhook writes these) ───────────────────
orders (
  id                 uuid pk default gen_random_uuid(),
  user_id            uuid references auth.users(id) on delete set null, -- null = guest
  stripe_session_id  text unique not null,     -- idempotency key
  status             text not null default 'pending', -- pending|paid|shipped|delivered|cancelled|refunded
  email              text,
  customer_name      text,
  customer_phone     text,
  subtotal           numeric(10,2) not null default 0,
  shipping_amount    numeric(10,2) not null default 0,
  total              numeric(10,2) not null default 0,
  currency           text not null default 'eur',
  carrier            text,                     -- 'econt' | 'speedy' | 'boxnow' (chosen at checkout)
  shipping_method    text,                     -- 'office' | 'address' | 'apm'
  shipping           jsonb,                    -- address/city/postal/country + carrier-specific (office_code, apm id, site id), weight
  created_at         timestamptz not null default now(),
  paid_at            timestamptz
)

order_items (
  id           uuid pk default gen_random_uuid(),
  order_id     uuid not null references orders(id) on delete cascade,
  product_id   text references products(id) on delete set null,
  name         text not null,           -- snapshot (product may change/delete later)
  unit_price   numeric(10,2) not null,  -- snapshot
  quantity     int not null,
  config       jsonb                    -- customization at purchase (filament/text/size)
)

-- ── shipment (carrier-agnostic) ────────────────────────────
shipments (
  id            uuid pk default gen_random_uuid(),
  order_id      uuid not null unique references orders(id) on delete cascade,
  carrier       text not null,            -- 'econt' | 'speedy' | 'boxnow'
  service_type  text,                     -- 'office' | 'address' | 'apm'
  waybill       text,                     -- tracking/waybill number (any carrier)
  tracking_url  text,
  label_pdf_url text,
  status        text default 'created',   -- created|in_transit|delivered|returned|cancelled
  carrier_data  jsonb,                    -- carrier-specific blob (econt office_code, boxnow apm id, speedy site id…)
  cost          numeric(10,2),
  created_at    timestamptz not null default now()
)

-- ── AI gen (PLACEHOLDER — phase 2, table only) ─────────────
ai_generations (
  id               uuid pk default gen_random_uuid(),
  user_id          uuid not null references auth.users(id) on delete cascade,
  input_image_url  text,           -- ai-uploads bucket
  output_model_url text,           -- glb result
  status           text not null default 'queued', -- queued|processing|done|failed
  error            text,
  created_at       timestamptz not null default now()
)
```

### Relations
- `profiles 1—1 auth.users` (role lives here)
- `auth.users 1—* orders` (nullable → guest allowed)
- `orders 1—* order_items`, `orders 1—1 shipments`
- `products 1—* order_items` / `designs` (`on delete set null` — orders keep snapshot name+price)
- `auth.users 1—* designs`, `1—* ai_generations`

### Design choices
- **Snapshot** `name` + `unit_price` into `order_items` so orders stay correct after product edits/deletes (go-live must-have).
- `products.id` stays a `text` slug — matches current code, zero data migration.
- `is_active` soft-delete — never hard-delete a product referenced by orders.
- **Carrier-agnostic shipping**: `orders.carrier` + `orders.shipping` (jsonb) + `shipments.carrier_data` (jsonb). Adding Speedy / BoxNow = new code path + new enum value, **no migration**.
- **Analytics = SQL views/queries** over `orders` + `order_items` (revenue by day, top products, order counts, AOV). No extra tables in phase 1.

## 5. Row-Level Security (RLS) — DRAFT, pending approval

RLS ON for all tables (defense-in-depth; Api uses service-role which bypasses RLS for trusted writes).

| Table | customer (own) | public/anon | admin |
|---|---|---|---|
| `products` | read (is_active) | read (is_active) | full CRUD |
| `profiles` | read/update own | — | read all |
| `designs` | full CRUD own rows | — | read all |
| `orders` | read own rows | — | read/update all |
| `order_items` | read via own order | — | read all |
| `shipments` | read via own order | — | read/update all |
| `ai_generations` | full CRUD own rows | — | read all |

- "admin" = `exists (select 1 from profiles where id = auth.uid() and role='admin')`.
- Since the Api mediates everything with the service-role key, RLS is the backstop, not the primary gate. Primary gate = Api validating JWT + role.

## 6. New Functionalities / Work Items — DRAFT, pending approval

### 6.1 Api (server)
- **Supabase data layer**: add `supabase-csharp` (or raw PostgREST/Npgsql) + service-role key from config/secrets. New repositories: `ProductRepository`, `OrderRepository`, `DesignRepository`, `ProfileRepository`.
- **JWT auth middleware**: validate Supabase JWT, resolve `profiles.role`, `[Authorize]` + admin policy.
- **Stripe webhook** endpoint (`POST /api/stripe/webhook`): on `checkout.session.completed`, idempotently write `orders` + `order_items` (snapshot), set status `paid`, `paid_at`. Trigger label booking here (moves off page-load).
- **`IShippingProvider` abstraction**: refactor concrete `EcontService` behind an interface (`QuoteAsync`, `CreateLabelAsync`, `TrackAsync`, `GetOfficesAsync`/APMs). Register per-carrier impls; checkout/label code selects by `carrier`. Lets Speedy + BoxNow drop in later.
- **Server-side re-pricing**: compute shipping cost server-side (resolves existing TODO) instead of trusting client amount.
- **Product endpoints**: public read (active); admin CRUD.
- **Order endpoints**: customer reads own; admin reads/updates all.
- **Design endpoints**: customer CRUD own.
- **Analytics endpoints**: admin-only aggregate queries.

### 6.2 Web (browser)
- **Auth UI**: register / login / logout, password reset. Supabase Auth via `supabase-csharp` (anon key) or a thin Api-proxied auth. Store JWT, attach to Api `HttpClient`.
- **`AuthStateProvider`** + auth-aware NavMenu (login state, "My Orders", "My Designs", admin link if admin).
- **Replace `MockProductService`** with `ApiProductService` calling the Api (keep `IProductService` interface — drop-in).
- **Account pages**: order history, saved designs (load a design back into Studio).
- **Save-design** action in Studio (logged-in only).
- **Admin console** (route-guarded to admin): products table + editor (CRUD, upload image/model to Storage), orders table + status updates, analytics dashboard (revenue, top products, counts, AOV).

### 6.3 Storage
- Buckets `product-images`, `models`, `ai-uploads`. Admin uploads product image + model files; public read for display assets.

## 7. Phasing

- **Phase 0 — Foundation**: Supabase project, schema migration SQL, RLS, `profiles` signup trigger, seed `products` from current `MockProductService` data. Api Supabase data layer + config.
- **Phase 1 — Persistence + Auth**: JWT middleware, Stripe webhook → orders, `ApiProductService` replaces mock, auth UI + account pages (order history, saved designs).
- **Phase 2 — Admin console**: product CRUD + Storage uploads, order management, analytics dashboards.
- **Phase 3 — Multi-carrier shipping**: `IShippingProvider` refactor of Econt, add Speedy + BoxNow/EasyBox (APM) providers.
- **Phase 4 — AI 3D-gen** (separate spec): wire `ai_generations`, choose build-vs-buy, rate-limit/abuse-prevention, ai-uploads bucket, Studio integration.

Each phase gets its own implementation plan (writing-plans) when reached.

## 8. Error Handling & Testing (high level)

- **Webhook**: verify Stripe signature; idempotent on `stripe_session_id` (unique constraint + upsert); on failure return 5xx so Stripe retries.
- **Auth**: reject expired/invalid JWT; admin endpoints double-check role server-side (never trust client claims alone).
- **Shipping**: provider errors return a clear problem result; never block order creation on label failure (book label async / retriable).
- **Testing**: unit-test repositories + pricing + webhook idempotency; the existing `tests/MyMiniCar.Tests` project (xUnit) is the home for these.

## 9. Open Questions / TBD

- **Auth delivery**: Supabase Auth directly from Blazor (`supabase-csharp`, anon key) vs. proxying auth through our Api. (Leaning: direct Supabase Auth for login/session, Api validates JWT.)
- **Currency**: Stripe currently `eur`; confirm store currency / multi-currency need.
- **Admin count**: single admin (you) vs. multiple — affects whether an admin-invite flow is needed (default: manually set `role='admin'` in DB).
- **AI build-vs-buy** (phase 4): external API (Meshy/Tripo/Luma) vs self-hosted model.
- **Hosting**: Api must be always-on (Render web service); confirm plan/cost. Web stays static site.

---

*Next step after approval: invoke `writing-plans` to produce the Phase 0 + Phase 1 implementation plan.*
