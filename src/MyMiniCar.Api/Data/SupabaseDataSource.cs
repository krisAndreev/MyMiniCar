using Npgsql;

namespace MyMiniCar.Api.Data;

/// <summary>
/// Owns the single Npgsql data source for server-side Postgres access against
/// the Supabase database. Uses the Supabase Postgres connection string (service
/// credentials) — never exposed to the browser. All Api repositories share this.
/// </summary>
public sealed class SupabaseDataSource : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public SupabaseDataSource(IConfiguration config)
    {
        var conn = config["Supabase:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Supabase:ConnectionString not configured. Run: dotnet user-secrets set " +
                "\"Supabase:ConnectionString\" \"Host=db.<ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true\"");

        _dataSource = NpgsqlDataSource.Create(conn);
    }

    public NpgsqlDataSource DataSource => _dataSource;

    /// <summary>Opens a connection and runs `select 1` to prove connectivity.</summary>
    public async Task<bool> CanConnectAsync(CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand("select 1");
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is 1;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
