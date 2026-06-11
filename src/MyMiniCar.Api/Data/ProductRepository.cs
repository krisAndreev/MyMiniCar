using MyMiniCar.Api.Models;
using Npgsql;

namespace MyMiniCar.Api.Data;

/// <summary>Read-only access to the products catalog.</summary>
public sealed class ProductRepository
{
    private readonly SupabaseDataSource _db;

    public ProductRepository(SupabaseDataSource db) => _db = db;

    private const string SelectColumns = @"
        id, name, description, name_bg, description_bg, price,
        coalesce(image_url, '')        as image_url,
        category,
        coalesce(default_material, '') as default_material,
        coalesce(dimensions, '')       as dimensions,
        is_featured, weight_grams,
        coalesce(tile_class, 'fil-blue') as tile_class";

    public Task<List<ProductDto>> GetActiveAsync(CancellationToken ct = default) =>
        QueryAsync($"select {SelectColumns} from public.products where is_active order by sort_order, name", null, ct);

    public Task<List<ProductDto>> GetFeaturedAsync(CancellationToken ct = default) =>
        QueryAsync($"select {SelectColumns} from public.products where is_active and is_featured order by sort_order, name", null, ct);

    public async Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var rows = await QueryAsync($"select {SelectColumns} from public.products where id = $1 and is_active", id, ct);
        return rows.FirstOrDefault();
    }

    private async Task<List<ProductDto>> QueryAsync(string sql, string? idParam, CancellationToken ct)
    {
        await using var cmd = _db.DataSource.CreateCommand(sql);
        if (idParam is not null) cmd.Parameters.AddWithValue(idParam);

        var list = new List<ProductDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProductDto(
                Id:              reader.GetString(0),
                Name:            reader.GetString(1),
                Description:     reader.GetString(2),
                NameBg:          reader.IsDBNull(3) ? null : reader.GetString(3),
                DescriptionBg:   reader.IsDBNull(4) ? null : reader.GetString(4),
                Price:           reader.GetDecimal(5),
                ImageUrl:        reader.GetString(6),
                Category:        reader.GetString(7),
                DefaultMaterial: reader.GetString(8),
                Dimensions:      reader.GetString(9),
                IsFeatured:      reader.GetBoolean(10),
                WeightGrams:     reader.GetInt32(11),
                TileClass:       reader.GetString(12)));
        }
        return list;
    }
}
