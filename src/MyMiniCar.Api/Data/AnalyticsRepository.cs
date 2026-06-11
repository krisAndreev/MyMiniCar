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
