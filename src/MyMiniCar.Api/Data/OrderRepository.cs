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
}
