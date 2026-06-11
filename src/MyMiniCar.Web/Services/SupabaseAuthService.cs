using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MyMiniCar.Web.Services;

/// <summary>Signup/login/logout against Supabase Auth (GoTrue) REST, using the
/// public anon key. On success the access token is saved to the TokenStore.</summary>
public sealed class SupabaseAuthService
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;
    private readonly string _anonKey;

    public SupabaseAuthService(string supabaseUrl, string anonKey, TokenStore tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri($"{supabaseUrl.TrimEnd('/')}/auth/v1/") };
        _anonKey = anonKey;
        _http.DefaultRequestHeaders.Add("apikey", anonKey);
        _tokens = tokens;
    }

    public Task<string?> RegisterAsync(string email, string password, string? fullName)
        => PostAuthAsync("signup", new { email, password, data = new { full_name = fullName } });

    public Task<string?> LoginAsync(string email, string password)
        => PostAuthAsync("token?grant_type=password", new { email, password });

    public ValueTask LogoutAsync() => _tokens.ClearAsync();

    /// <summary>Returns an error message on failure, or null on success.</summary>
    private async Task<string?> PostAuthAsync(string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadFromJsonAsync<GoTrueError>();
            return err?.Message ?? err?.ErrorDescription ?? $"Auth failed ({(int)resp.StatusCode}).";
        }

        var session = await resp.Content.ReadFromJsonAsync<GoTrueSession>();
        if (string.IsNullOrEmpty(session?.AccessToken))
            return "No session returned — is email confirmation required?";

        await _tokens.SetAsync(session.AccessToken);
        return null;
    }

    private sealed record GoTrueSession(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);

    private sealed record GoTrueError(
        [property: JsonPropertyName("msg")] string? Message,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
