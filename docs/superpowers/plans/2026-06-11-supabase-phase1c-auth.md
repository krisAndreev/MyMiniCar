# Supabase Phase 1C — Auth Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.
>
> **Execution rule (token-safety):** ONE task per turn. Each task ends in a git commit + ticked boxes. Resume: open this file, find first unchecked box. Working tree clean between tasks.

**Goal:** Customers can register and log in. The browser obtains a Supabase Auth JWT; the Api validates it and exposes the caller's identity + role. This is the auth backbone; account pages + order→user linking come in Phase 1D.

**Architecture:** Browser talks to Supabase Auth's REST endpoints directly (`/auth/v1/signup`, `/auth/v1/token`) using the public **anon key** — no Supabase C# SDK (it needs net9; we're net7). The returned JWT is stored in `localStorage` (JS interop) and surfaced to Blazor via a custom `AuthenticationStateProvider`. The Api validates the JWT with the project **JWT secret** (HS256) using `Microsoft.AspNetCore.Authentication.JwtBearer`, and reads `profiles.role` for authorization.

**Tech Stack:** Blazor WASM (.NET 7), `Microsoft.AspNetCore.Components.Authorization`, ASP.NET `JwtBearer` (net7), Npgsql, Supabase Auth (GoTrue) REST.

**Spec:** `docs/superpowers/specs/2026-06-11-supabase-db-integration-design.md`
**Prereq:** Phase 0/1A/1B complete.

---

## USER ACTIONS

From Supabase → Settings → API:
- **Project URL** (e.g. `https://sdiirjjagjqqvqfexohq.supabase.co`)
- **anon public key** (a long JWT-looking string, safe for the browser)
- **JWT Secret** (Settings → API → JWT Settings) — server-only

Set them:
```bash
# Api (server) — validates tokens
cd src/MyMiniCar.Api
dotnet user-secrets set "Supabase:Url" "https://<ref>.supabase.co"
dotnet user-secrets set "Supabase:JwtSecret" "<jwt-secret>"
```
For the Web (browser), add to `src/MyMiniCar.Web/wwwroot/appsettings.json` (anon key is public, safe to ship):
```json
{
  "ApiBaseUrl": "http://localhost:5230",
  "Supabase": {
    "Url": "https://<ref>.supabase.co",
    "AnonKey": "<anon-public-key>"
  }
}
```
(If that file does not exist yet, Task 2 Step 1 creates it.)

Also in Supabase → Authentication → Providers → Email: for local dev, turn **OFF** "Confirm email" so logins work immediately without an email round-trip (turn it back on for production).

---

## File Structure

- Modify: `src/MyMiniCar.Api/MyMiniCar.Api.csproj` — add `Microsoft.AspNetCore.Authentication.JwtBearer`
- Create: `src/MyMiniCar.Api/Data/ProfileRepository.cs` — role lookup
- Modify: `src/MyMiniCar.Api/Program.cs` — JwtBearer config, auth middleware, `/api/auth/me`
- Create: `src/MyMiniCar.Web/wwwroot/appsettings.json` — Supabase url + anon key (if missing)
- Create: `src/MyMiniCar.Web/Services/SupabaseAuthService.cs` — signup/login/logout via GoTrue REST
- Create: `src/MyMiniCar.Web/Services/TokenStore.cs` — localStorage-backed token persistence
- Create: `src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs` — AuthenticationStateProvider
- Modify: `src/MyMiniCar.Web/Program.cs` — register auth services + AuthorizationCore
- Modify: `src/MyMiniCar.Web/App.razor` — CascadingAuthenticationState + AuthorizeRouteView
- Modify: `src/MyMiniCar.Web/_Imports.razor` — add Authorization usings
- Create: `src/MyMiniCar.Web/Pages/Login.razor` and `Register.razor`
- Modify: `src/MyMiniCar.Web/Shared/NavMenu.razor` — login/logout display

---

## Task 1: Api JWT validation + /api/auth/me

**Files:**
- Modify: `src/MyMiniCar.Api/MyMiniCar.Api.csproj`
- Create: `src/MyMiniCar.Api/Data/ProfileRepository.cs`
- Modify: `src/MyMiniCar.Api/Program.cs`

- [x] **Step 1: Add the JwtBearer package**

Run:
```bash
cd src/MyMiniCar.Api
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 7.0.20
```
Expected: package added to csproj, restore succeeds.

- [x] **Step 2: Create ProfileRepository**

`src/MyMiniCar.Api/Data/ProfileRepository.cs`:
```csharp
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
```

- [x] **Step 3: Configure auth in Program.cs**

Add usings at the top of `src/MyMiniCar.Api/Program.cs`:
```csharp
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```
(`System.Text` may already be imported — do not duplicate it.)

Register the repo + auth, next to the other `AddScoped` calls (before `var app = builder.Build();`):
```csharp
builder.Services.AddScoped<ProfileRepository>();

var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url not configured.");
var jwtSecret = builder.Configuration["Supabase:JwtSecret"]
    ?? throw new InvalidOperationException("Supabase:JwtSecret not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl.TrimEnd('/')}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();
```

Add the middleware right after `app.UseCors(CorsPolicy);`:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [x] **Step 4: Add the /api/auth/me endpoint**

In `src/MyMiniCar.Api/Program.cs`, add near the product endpoints:
```csharp
app.MapGet("/api/auth/me", async (ClaimsPrincipal user, ProfileRepository profiles) =>
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? user.FindFirstValue("sub");
    if (sub is null || !Guid.TryParse(sub, out var userId))
        return Results.Unauthorized();

    var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
    var role = await profiles.GetRoleAsync(userId);
    return Results.Ok(new { id = userId, email, role });
}).RequireAuthorization();
```

- [x] **Step 5: Build + verify unauthorized is rejected**

Run:
```bash
cd src/MyMiniCar.Api && dotnet build
```
Expected: Build succeeded, 0 errors. (Requires `Supabase:Url` + `Supabase:JwtSecret` set, per USER ACTIONS, for the app to start — build itself does not need them.)

- [x] **Step 6: Commit**

```bash
git add src/MyMiniCar.Api/MyMiniCar.Api.csproj src/MyMiniCar.Api/Data/ProfileRepository.cs src/MyMiniCar.Api/Program.cs
git commit -m "feat(api): validate Supabase JWT + /api/auth/me (role-aware)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Web token store + Supabase auth service

**Files:**
- Create: `src/MyMiniCar.Web/wwwroot/appsettings.json` (if missing)
- Create: `src/MyMiniCar.Web/Services/TokenStore.cs`
- Create: `src/MyMiniCar.Web/Services/SupabaseAuthService.cs`

- [ ] **Step 1: Ensure appsettings.json exists**

If `src/MyMiniCar.Web/wwwroot/appsettings.json` does not exist, create it (real values per USER ACTIONS):
```json
{
  "ApiBaseUrl": "http://localhost:5230",
  "Supabase": {
    "Url": "https://<ref>.supabase.co",
    "AnonKey": "<anon-public-key>"
  }
}
```
If it already exists, add the `Supabase` section to it.

- [ ] **Step 2: Create the token store**

`src/MyMiniCar.Web/Services/TokenStore.cs`:
```csharp
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
```

- [ ] **Step 3: Create the auth service**

`src/MyMiniCar.Web/Services/SupabaseAuthService.cs`:
```csharp
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
```

- [ ] **Step 4: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/MyMiniCar.Web/wwwroot/appsettings.json src/MyMiniCar.Web/Services/TokenStore.cs src/MyMiniCar.Web/Services/SupabaseAuthService.cs
git commit -m "feat(web): Supabase auth service + token store

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: AuthenticationStateProvider + DI wiring

**Files:**
- Create: `src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs`
- Modify: `src/MyMiniCar.Web/Program.cs`
- Modify: `src/MyMiniCar.Web/_Imports.razor`
- Modify: `src/MyMiniCar.Web/App.razor`

- [ ] **Step 1: Create the state provider**

`src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs`:
```csharp
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
```

- [ ] **Step 2: Add the JWT package for token parsing**

Run:
```bash
cd src/MyMiniCar.Web
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.1.2
```
Expected: package added; restore succeeds.

- [ ] **Step 3: Wire DI in Program.cs**

In `src/MyMiniCar.Web/Program.cs`, add usings at the top:
```csharp
using Microsoft.AspNetCore.Components.Authorization;
using MyMiniCar.Web.Auth;
```
Register services (after the existing service registrations, using the already-declared `apiBaseUrl`; read Supabase config from configuration):
```csharp
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "";
var supabaseAnon = builder.Configuration["Supabase:AnonKey"] ?? "";

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped(sp => new SupabaseAuthService(
    supabaseUrl, supabaseAnon, sp.GetRequiredService<TokenStore>()));
builder.Services.AddScoped<SupabaseAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<SupabaseAuthStateProvider>());
builder.Services.AddAuthorizationCore();
```

- [ ] **Step 4: Add usings to _Imports.razor**

Append to `src/MyMiniCar.Web/_Imports.razor`:
```razor
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Authorization
```

- [ ] **Step 5: Wrap the router in App.razor**

Replace the contents of `src/MyMiniCar.Web/App.razor` with (preserving the existing `AppAssembly`/layout references):
```razor
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
            <FocusOnNavigate RouteData="@routeData" Selector="h1" />
        </Found>
        <NotFound>
            <PageTitle>Not found</PageTitle>
            <LayoutView Layout="@typeof(MainLayout)">
                <p role="alert">Sorry, there's nothing at this address.</p>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```
NOTE: if the current `App.razor` references a different default layout than `MainLayout`, keep that one.

- [ ] **Step 6: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/MyMiniCar.Web/Auth/SupabaseAuthStateProvider.cs src/MyMiniCar.Web/MyMiniCar.Web.csproj src/MyMiniCar.Web/Program.cs src/MyMiniCar.Web/_Imports.razor src/MyMiniCar.Web/App.razor
git commit -m "feat(web): Supabase AuthenticationStateProvider + authz wiring

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Login + Register pages + NavMenu

**Files:**
- Create: `src/MyMiniCar.Web/Pages/Login.razor`
- Create: `src/MyMiniCar.Web/Pages/Register.razor`
- Modify: `src/MyMiniCar.Web/Shared/NavMenu.razor`

- [ ] **Step 1: Create Login.razor**

`src/MyMiniCar.Web/Pages/Login.razor`:
```razor
@page "/login"
@using MyMiniCar.Web.Auth
@using MyMiniCar.Web.Services
@inject SupabaseAuthService Auth
@inject SupabaseAuthStateProvider AuthState
@inject NavigationManager Nav

<PageTitle>Log in</PageTitle>
<div class="auth-card">
    <h1>Log in</h1>
    <input type="email" placeholder="Email" @bind="_email" />
    <input type="password" placeholder="Password" @bind="_password" />
    <button disabled="@_busy" @onclick="DoLogin">@(_busy ? "..." : "Log in")</button>
    @if (_error is not null) { <p class="auth-error">@_error</p> }
    <p>No account? <a href="/register">Register</a></p>
</div>

@code {
    private string _email = "", _password = "";
    private string? _error;
    private bool _busy;

    private async Task DoLogin()
    {
        _busy = true; _error = null;
        _error = await Auth.LoginAsync(_email, _password);
        _busy = false;
        if (_error is null)
        {
            AuthState.NotifyChanged();
            Nav.NavigateTo("/");
        }
    }
}
```

- [ ] **Step 2: Create Register.razor**

`src/MyMiniCar.Web/Pages/Register.razor`:
```razor
@page "/register"
@using MyMiniCar.Web.Auth
@using MyMiniCar.Web.Services
@inject SupabaseAuthService Auth
@inject SupabaseAuthStateProvider AuthState
@inject NavigationManager Nav

<PageTitle>Register</PageTitle>
<div class="auth-card">
    <h1>Create account</h1>
    <input placeholder="Full name" @bind="_name" />
    <input type="email" placeholder="Email" @bind="_email" />
    <input type="password" placeholder="Password" @bind="_password" />
    <button disabled="@_busy" @onclick="DoRegister">@(_busy ? "..." : "Register")</button>
    @if (_error is not null) { <p class="auth-error">@_error</p> }
    <p>Already have an account? <a href="/login">Log in</a></p>
</div>

@code {
    private string _name = "", _email = "", _password = "";
    private string? _error;
    private bool _busy;

    private async Task DoRegister()
    {
        _busy = true; _error = null;
        _error = await Auth.RegisterAsync(_email, _password, _name);
        _busy = false;
        if (_error is null)
        {
            AuthState.NotifyChanged();
            Nav.NavigateTo("/");
        }
    }
}
```

- [ ] **Step 3: Add login/logout to NavMenu**

In `src/MyMiniCar.Web/Shared/NavMenu.razor`, add an auth-aware block (place inside the existing nav markup where appropriate). At the top of the file add `@using` lines if not present:
```razor
@using Microsoft.AspNetCore.Components.Authorization
@using MyMiniCar.Web.Auth
@using MyMiniCar.Web.Services
@inject SupabaseAuthService Auth
@inject SupabaseAuthStateProvider AuthState
@inject NavigationManager Nav
```
Markup:
```razor
<AuthorizeView>
    <Authorized>
        <span class="nav-user">@context.User.FindFirst("email")?.Value</span>
        <button class="nav-logout" @onclick="Logout">Log out</button>
    </Authorized>
    <NotAuthorized>
        <a href="/login">Log in</a>
    </NotAuthorized>
</AuthorizeView>
```
Code block (merge into the existing `@code` if present):
```razor
@code {
    private async Task Logout()
    {
        await Auth.LogoutAsync();
        AuthState.NotifyChanged();
        Nav.NavigateTo("/");
    }
}
```

- [ ] **Step 4: Build**

Run: `cd src/MyMiniCar.Web && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/MyMiniCar.Web/Pages/Login.razor src/MyMiniCar.Web/Pages/Register.razor src/MyMiniCar.Web/Shared/NavMenu.razor
git commit -m "feat(web): login + register pages, nav auth state

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Live auth verification (needs keys)

**Files:** none (verification). Requires USER ACTIONS (Supabase url, anon key, JWT secret) set.

- [ ] **Step 1: Register a test user end-to-end**

Run the Api + Web, open the site, go to `/register`, create a test account.
Expected: redirected home; NavMenu shows the email + "Log out".

- [ ] **Step 2: Verify the Api accepts the token**

With the Web app open and logged in, copy the token from devtools (`localStorage.mmc_access_token`) and:
```bash
TOKEN="<paste>"
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5230/api/auth/me
```
Expected: `{"id":"<uuid>","email":"<you>","role":"customer"}`.

- [ ] **Step 3: Verify a profile row was auto-created**

```bash
export PGPASSWORD='<db-password>'
psql "host=aws-1-eu-north-1.pooler.supabase.com port=5432 dbname=postgres user=postgres.sdiirjjagjqqvqfexohq sslmode=require" \
  -t -c "select id, role from public.profiles order by created_at desc limit 1;"
```
Expected: one row (created by the `on_auth_user_created` trigger from Phase 0).

- [ ] **Step 4: Mark plan complete + commit**

Tick all boxes, then:
```bash
git add docs/superpowers/plans/2026-06-11-supabase-phase1c-auth.md
git commit -m "docs: Phase 1C complete"
```

---

## Self-Review (plan-write time)

- **Spec coverage:** "auth UI (register/login/logout), AuthStateProvider, auth-aware NavMenu, JWT middleware + role lookup" (spec §6.1/§6.2) ✓ Tasks 1–4. Order history + saved designs + order→user linking = Phase 1D.
- **Placeholders:** none — all code complete. `<ref>`/`<anon-public-key>`/`<jwt-secret>` are USER-supplied secrets, not code placeholders.
- **Type consistency:** `SupabaseAuthService` ctor `(supabaseUrl, anonKey, TokenStore)` matches the DI registration (Task 3 Step 3); `SupabaseAuthStateProvider.NotifyChanged()` called from Login/Register/NavMenu; `/api/auth/me` reads `sub`/`email` claims that Supabase JWTs carry.
- **Net7 safety:** no Supabase C# SDK used; browser hits GoTrue REST directly; `System.IdentityModel.Tokens.Jwt` 7.1.2 + JwtBearer 7.0.20 are net7-compatible.

## Phase 1C Done =
Tasks 1–4 committed; both projects build green; (after USER ACTIONS) register/login works, NavMenu reflects auth, `/api/auth/me` returns the role, and a `profiles` row auto-appears.

## Next: Phase 1D — Account pages + order linking
Attach the JWT to Api-bound HttpClients; `GET /api/orders/mine`; order-history + saved-designs pages; stamp `orders.user_id` at checkout when the buyer is logged in.
```
