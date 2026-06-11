using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MyMiniCar.Web.Services;

namespace MyMiniCar.Web.Auth;

/// <summary>Derives the Blazor auth state from the JWT in the TokenStore.</summary>
public sealed class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokens;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public SupabaseAuthStateProvider(TokenStore tokens) => _tokens = tokens;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        JwtSecurityToken jwt;
        try { jwt = new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch { return Anonymous; }

        if (jwt.ValidTo < DateTime.UtcNow) return Anonymous;

        var identity = new ClaimsIdentity(jwt.Claims, "supabase", "email", "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Call after login/logout so the UI re-evaluates auth.</summary>
    public void NotifyChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
