# Supabase

## Apply migrations
Paste each file in `migrations/` into the Supabase SQL editor in numeric order
(`0001` → `0002` → `0003`), then run `seed.sql`. Or with the Supabase CLI:
`supabase db push` then `psql "$DB_URL" -f seed.sql`.

## Api secret (never commit)
The Api talks to Postgres directly via Npgsql. Get the connection string from
Supabase → Settings → Database → Connection string (URI / .NET).

```
cd src/MyMiniCar.Api
dotnet user-secrets set "Supabase:ConnectionString" \
  "Host=db.<ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true"
```

On Render, set the same value as an environment variable named
`Supabase__ConnectionString` (double underscore).

## Verify
Run the Api, then `GET /api/health/db` → `{"db":"ok"}`.
After seeding: `select count(*) from products;` → 8.
