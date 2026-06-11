using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MyMiniCar.Web.Services;

namespace MyMiniCar.Web.Auth;

/// <summary>Derives Blazor auth state from the JWT, enriched with the app role
/// (profiles.role) fetched once per token from /api/auth/me.</summary>
public sealed class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokens;
    private readonly HttpClient _http;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _cachedToken;
    private string _cachedRole = "customer";

    public SupabaseAuthStateProvider(string apiBaseUrl, TokenStore tokens)
    {
        _tokens = tokens;
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        JwtSecurityToken jwt;
        try { jwt = new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch { return Anonymous; }
        if (jwt.ValidTo < DateTime.UtcNow) return Anonymous;

        var role = await GetAppRoleAsync(token);
        var claims = new List<Claim>(jwt.Claims) { new(ClaimTypes.Role, role) };
        var identity = new ClaimsIdentity(claims, "supabase", "email", ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private async Task<string> GetAppRoleAsync(string token)
    {
        if (token == _cachedToken) return _cachedRole;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var me = await resp.Content.ReadFromJsonAsync<MeResponse>();
                _cachedRole = me?.Role ?? "customer";
                _cachedToken = token;
            }
        }
        catch { /* keep previous role on transient failure */ }
        return _cachedRole;
    }

    public void NotifyChanged()
    {
        _cachedToken = null; // force a role refresh on next evaluation
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private sealed record MeResponse(string Id, string? Email, string Role);
}
