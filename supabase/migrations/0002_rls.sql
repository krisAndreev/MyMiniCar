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
