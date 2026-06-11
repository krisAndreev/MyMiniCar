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
create index if not exists idx_orders_user      on public.orders(user_id);
create index if not exists idx_orders_status     on public.orders(status);
create index if not exists idx_order_items_order on public.order_items(order_id);
create index if not exists idx_designs_user      on public.designs(user_id);
create index if not exists idx_products_active    on public.products(is_active);
