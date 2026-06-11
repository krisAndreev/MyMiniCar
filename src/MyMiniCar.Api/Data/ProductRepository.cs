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

    private const string AdminColumns = @"
        id, name, description, name_bg, description_bg, price, category,
        image_url, display_model_url, print_model_url, default_material, dimensions,
        weight_grams, coalesce(tile_class,'fil-blue'), is_featured, is_active, sort_order";

    public async Task<List<AdminProductView>> GetAllAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            $"select {AdminColumns} from public.products order by sort_order, name");
        var list = new List<AdminProductView>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(MapAdmin(r));
        return list;
    }

    private static AdminProductView MapAdmin(System.Data.Common.DbDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2),
        r.IsDBNull(3) ? null : r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        r.GetDecimal(5), r.GetString(6),
        r.IsDBNull(7) ? null : r.GetString(7),
        r.IsDBNull(8) ? null : r.GetString(8),
        r.IsDBNull(9) ? null : r.GetString(9),
        r.IsDBNull(10) ? null : r.GetString(10),
        r.IsDBNull(11) ? null : r.GetString(11),
        r.GetInt32(12), r.GetString(13),
        r.GetBoolean(14), r.GetBoolean(15), r.GetInt32(16));

    /// <summary>Insert or update a product by id.</summary>
    public async Task UpsertAsync(ProductWrite p, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(@"
            insert into public.products
              (id, name, description, name_bg, description_bg, price, category,
               image_url, display_model_url, print_model_url, default_material, dimensions,
               weight_grams, tile_class, is_featured, is_active, sort_order, updated_at)
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17, now())
            on conflict (id) do update set
              name=excluded.name, description=excluded.description,
              name_bg=excluded.name_bg, description_bg=excluded.description_bg,
              price=excluded.price, category=excluded.category, image_url=excluded.image_url,
              display_model_url=excluded.display_model_url, print_model_url=excluded.print_model_url,
              default_material=excluded.default_material, dimensions=excluded.dimensions,
              weight_grams=excluded.weight_grams, tile_class=excluded.tile_class,
              is_featured=excluded.is_featured, is_active=excluded.is_active,
              sort_order=excluded.sort_order, updated_at=now()");
        cmd.Parameters.AddWithValue(p.Id);
        cmd.Parameters.AddWithValue(p.Name);
        cmd.Parameters.AddWithValue(p.Description);
        cmd.Parameters.AddWithValue((object?)p.NameBg ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.DescriptionBg ?? DBNull.Value);
        cmd.Parameters.AddWithValue(p.Price);
        cmd.Parameters.AddWithValue(p.Category);
        cmd.Parameters.AddWithValue((object?)p.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.DisplayModelUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.PrintModelUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.DefaultMaterial ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)p.Dimensions ?? DBNull.Value);
        cmd.Parameters.AddWithValue(p.WeightGrams);
        cmd.Parameters.AddWithValue(string.IsNullOrWhiteSpace(p.TileClass) ? "fil-blue" : p.TileClass);
        cmd.Parameters.AddWithValue(p.IsFeatured);
        cmd.Parameters.AddWithValue(p.IsActive);
        cmd.Parameters.AddWithValue(p.SortOrder);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> SetActiveAsync(string id, bool active, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "update public.products set is_active=$2, updated_at=now() where id=$1");
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(active);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
