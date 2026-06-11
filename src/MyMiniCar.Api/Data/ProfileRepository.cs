using Npgsql;

namespace MyMiniCar.Api.Data;

/// <summary>Reads profile data (currently just the role) for authz decisions.</summary>
public sealed class ProfileRepository
{
    private readonly SupabaseDataSource _db;

    public ProfileRepository(SupabaseDataSource db) => _db = db;

    /// <summary>Returns the role for a user id, or "customer" if no row exists yet.</summary>
    public async Task<string> GetRoleAsync(Guid userId, CancellationToken ct = default)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "select role from public.profiles where id = $1");
        cmd.Parameters.AddWithValue(userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string ?? "customer";
    }
}
