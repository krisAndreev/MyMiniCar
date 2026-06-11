using Microsoft.JSInterop;

namespace MyMiniCar.Web.Services;

/// <summary>Persists the Supabase access token in browser localStorage.</summary>
public sealed class TokenStore
{
    private const string Key = "mmc_access_token";
    private readonly IJSRuntime _js;

    public TokenStore(IJSRuntime js) => _js = js;

    public ValueTask<string?> GetAsync()
        => _js.InvokeAsync<string?>("localStorage.getItem", Key);

    public ValueTask SetAsync(string token)
        => _js.InvokeVoidAsync("localStorage.setItem", Key, token);

    public ValueTask ClearAsync()
        => _js.InvokeVoidAsync("localStorage.removeItem", Key);
}
