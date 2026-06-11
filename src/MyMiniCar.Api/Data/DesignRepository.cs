using MyMiniCar.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace MyMiniCar.Api.Data;

/// <summary>Owner-scoped CRUD for saved Studio designs. config is stored as jsonb.</summary>
public sealed class DesignRepository
{
    private readonly SupabaseDataSource _db;

    public DesignRepository(SupabaseDataSource db) => _db = db;

    public async Task<Guid> CreateAsync(Guid userId, DesignCreate input, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(@"
            insert into public.designs (user_id, name, config, source)
            values ($1, $2, $3::jsonb, 'studio')
            returning id");
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue((object?)input.Name ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter { Value = input.ConfigJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<List<DesignView>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "select id, name, config::text, created_at from public.designs where user_id = $1 order by created_at desc");
        cmd.Parameters.AddWithValue(userId);

        var list = new List<DesignView>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new DesignView(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3)));
        return list;
    }

    /// <summary>Deletes a design only if it belongs to the user. Returns true if a row was removed.</summary>
    public async Task<bool> DeleteAsync(Guid userId, Guid designId, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "delete from public.designs where id = $1 and user_id = $2");
        cmd.Parameters.AddWithValue(designId);
        cmd.Parameters.AddWithValue(userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
