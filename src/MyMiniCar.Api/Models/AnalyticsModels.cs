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
