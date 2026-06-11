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
