# Studio Single-Screen Configurator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the multi-step Studio wizard with a single-screen, conversion-optimized configurator that has size tiers, realistic tiered materials shown live in the 3D preview, optional keychain-only engraving, and personal second-person copy.

**Architecture:** Blazor Server. New focused model files (`SizeOption`, `Material`, `StudioConfig`) replace the Studio half of `KeychainConfig.cs`; the existing `Filament`/`Filaments` move to their own file untouched (still used by Products/About/KeychainPreview). `Convert.razor` becomes one screen: sticky 3D preview + all controls + sticky add-to-cart bar, no stepper. The Three.js viewer gains a `setMaterial` PBR call so finish choice is visible live. A small xUnit project guards the price/gating math.

**Tech Stack:** .NET 7 / Blazor Server, Three.js (vendored), xUnit.

---

## File Structure

**Create:**
- `src/MyMiniCar.Web/Models/SizeOption.cs` — `SizeOption` record + `Sizes` catalog.
- `src/MyMiniCar.Web/Models/Material.cs` — `MaterialTier` enum, `Material` class, `Materials` catalog (8 studio finishes + PBR).
- `src/MyMiniCar.Web/Models/Filament.cs` — moved `Filament` + `Filaments` (verbatim, minus the now-unused `BasePrice`).
- `src/MyMiniCar.Web/Models/StudioConfig.cs` — replaces `KeychainConfig`.
- `tests/MyMiniCar.Tests/MyMiniCar.Tests.csproj` — xUnit project.
- `tests/MyMiniCar.Tests/StudioConfigTests.cs` — price + gating tests.

**Modify:**
- `src/MyMiniCar.Web/Pages/Convert.razor` — single-screen rewrite.
- `src/MyMiniCar.Web/Shared/GolfViewer.razor` — material params.
- `src/MyMiniCar.Web/wwwroot/js/golf-viewer.js` — `setMaterial`.
- `src/MyMiniCar.Web/wwwroot/css/app.css` — single-screen layout, sticky bar, size chips, `mat-*` swatches, collapsible engraving; remove dead stepper CSS.
- `MyMiniCar.sln` — add test project.

**Delete:**
- `src/MyMiniCar.Web/Models/KeychainConfig.cs` (Filament/Filaments moved out, KeychainConfig replaced by StudioConfig).

**Untouched (rely on `Filaments`/`fil-*` — do NOT migrate):** `Pages/Products.razor`, `Pages/About.razor`, `Shared/KeychainPreview.razor`, `Services/MockProductService.cs`, `Models/Product.cs`.

---

## Task 1: Size tiers model

**Files:**
- Create: `src/MyMiniCar.Web/Models/SizeOption.cs`

- [ ] **Step 1: Create the size model + catalog**

```csharp
namespace MyMiniCar.Web.Models;

/// <summary>A fixed product size/format the user can pick in the Studio.</summary>
/// <param name="Key">Stable id used in selection state.</param>
/// <param name="Name">Display name (e.g. "Keychain").</param>
/// <param name="Dimensions">Human-readable dimensions for the spec line.</param>
/// <param name="BasePrice">Base price before any material surcharge.</param>
/// <param name="HasRing">Whether the format includes a keyring.</param>
/// <param name="AllowsText">Whether engraving is offered (keychain only).</param>
/// <param name="Blurb">Short one-line description for the size chip.</param>
public record SizeOption(
    string Key, string Name, string Dimensions,
    decimal BasePrice, bool HasRing, bool AllowsText, string Blurb);

/// <summary>Catalog of selectable sizes. The keychain is the default.</summary>
public static class Sizes
{
    public static readonly IReadOnlyList<SizeOption> All = new[]
    {
        new SizeOption("keychain", "Keychain", "55 × 30 × 4 mm", 16.90m, true,  true,
            "Pocket-sized, with a keyring + text plate"),
        new SizeOption("desk",     "Desk Figure", "≈ 70 mm",      24.90m, false, false,
            "A standalone mini for your desk"),
        new SizeOption("display",  "Display Figure", "≈ 110 mm",  39.90m, false, false,
            "Bigger, with the finest detail"),
    };

    public static SizeOption Default => All[0];

    public static SizeOption ByKey(string key) =>
        All.FirstOrDefault(s => s.Key == key) ?? Default;
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`
Expected: Build succeeded (warnings ok).

- [ ] **Step 3: Commit**

```bash
git add src/MyMiniCar.Web/Models/SizeOption.cs
git commit -m "feat: add SizeOption model and Sizes catalog"
```

---

## Task 2: Materials model (realistic finishes + PBR)

**Files:**
- Create: `src/MyMiniCar.Web/Models/Material.cs`

- [ ] **Step 1: Create the material model + catalog**

`Surcharge` is derived from `Tier` via `TierSurcharge`. `FixedColor` is used by the preview when `!Paintable`. `Css` is a new `mat-*` swatch class (added in Task 6).

```csharp
namespace MyMiniCar.Web.Models;

public enum MaterialTier { Standard, Premium, Showcase }

/// <summary>A realistic print finish selectable in the Studio. PBR fields drive the live 3D preview.</summary>
public class Material
{
    public string Name { get; init; } = string.Empty;
    public MaterialTier Tier { get; init; }

    /// <summary>True = takes the chosen body colour; false = shows its own <see cref="FixedColor"/>.</summary>
    public bool Paintable { get; init; }

    /// <summary>Three.js MeshStandardMaterial metalness (0–1).</summary>
    public double Metalness { get; init; }
    /// <summary>Three.js MeshStandardMaterial roughness (0–1).</summary>
    public double Roughness { get; init; }
    /// <summary>Glow: emit the body colour.</summary>
    public bool Emissive { get; init; }

    /// <summary>Intrinsic colour shown in the preview when not paintable (hex).</summary>
    public string FixedColor { get; init; } = "#9aa3b0";

    /// <summary>CSS swatch class for the picker chip (e.g. "mat-pla").</summary>
    public string Css { get; init; } = string.Empty;

    public decimal Surcharge => Materials.TierSurcharge(Tier);
}

/// <summary>Single source of truth for Studio materials. Matte PLA is the default.</summary>
public static class Materials
{
    public static decimal TierSurcharge(MaterialTier tier) => tier switch
    {
        MaterialTier.Standard => 0m,
        MaterialTier.Premium  => 6m,
        MaterialTier.Showcase => 14m,
        _ => 0m,
    };

    public static readonly IReadOnlyList<Material> All = new[]
    {
        new Material { Name = "Matte PLA",         Tier = MaterialTier.Standard, Paintable = true,  Metalness = 0.00, Roughness = 0.85, Css = "mat-pla" },
        new Material { Name = "Glossy Resin",      Tier = MaterialTier.Standard, Paintable = true,  Metalness = 0.05, Roughness = 0.18, Css = "mat-resin" },
        new Material { Name = "Brushed Aluminium", Tier = MaterialTier.Premium,  Paintable = false, Metalness = 0.90, Roughness = 0.45, FixedColor = "#c8ccd2", Css = "mat-alu" },
        new Material { Name = "Wood Composite",    Tier = MaterialTier.Premium,  Paintable = false, Metalness = 0.00, Roughness = 0.90, FixedColor = "#9a6b3f", Css = "mat-wood" },
        new Material { Name = "Marble Composite",  Tier = MaterialTier.Premium,  Paintable = false, Metalness = 0.05, Roughness = 0.30, FixedColor = "#e8e6e2", Css = "mat-marble" },
        new Material { Name = "Glow in the Dark",  Tier = MaterialTier.Premium,  Paintable = true,  Metalness = 0.00, Roughness = 0.70, Emissive = true, Css = "mat-glow" },
        new Material { Name = "Die-cast Metal",    Tier = MaterialTier.Showcase, Paintable = false, Metalness = 1.00, Roughness = 0.30, FixedColor = "#3a3d42", Css = "mat-diecast" },
        new Material { Name = "Carbon Fiber",      Tier = MaterialTier.Showcase, Paintable = false, Metalness = 0.40, Roughness = 0.50, FixedColor = "#22242a", Css = "mat-carbon" },
    };

    public static Material Default => All[0];

    public static Material ByName(string name) =>
        All.FirstOrDefault(m => m.Name == name) ?? Default;
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/MyMiniCar.Web/Models/Material.cs
git commit -m "feat: add Material model with tiered finishes and PBR params"
```

---

## Task 3: Split out Filament, add StudioConfig, delete KeychainConfig

**Files:**
- Create: `src/MyMiniCar.Web/Models/Filament.cs`
- Create: `src/MyMiniCar.Web/Models/StudioConfig.cs`
- Delete: `src/MyMiniCar.Web/Models/KeychainConfig.cs`

- [ ] **Step 1: Create `Filament.cs` with the moved `Filament` + `Filaments`**

Verbatim move of the existing classes so Products/About/KeychainPreview keep working. The unused `BasePrice` constant is dropped (only `KeychainConfig` referenced it).

```csharp
using System.Collections.Generic;
using System.Linq;

namespace MyMiniCar.Web.Models;

/// <summary>A selectable PLA filament finish (used by the Products + About finish pickers).</summary>
public class Filament
{
    public string Name { get; init; } = string.Empty;
    /// <summary>CSS class for the swatch chip + plate tint (e.g. "fil-red").</summary>
    public string Css { get; init; } = string.Empty;
    /// <summary>Added cost over the base price.</summary>
    public decimal Surcharge { get; init; }
    public bool IsPremium { get; init; }
}

public static class Filaments
{
    public static readonly IReadOnlyList<Filament> All = new List<Filament>
    {
        new() { Name = "Midnight Black", Css = "fil-black" },
        new() { Name = "Pure White", Css = "fil-white" },
        new() { Name = "Racing Red", Css = "fil-red" },
        new() { Name = "Racing Blue", Css = "fil-blue" },
        new() { Name = "Silver Steel", Css = "fil-silver" },
        new() { Name = "Glow in the Dark", Css = "fil-glow", Surcharge = 4m, IsPremium = true },
        new() { Name = "Marble PLA", Css = "fil-marble", Surcharge = 5m, IsPremium = true },
        new() { Name = "Wood Fill", Css = "fil-wood", Surcharge = 5m, IsPremium = true }
    };

    public static Filament ByName(string name) =>
        All.FirstOrDefault(f => f.Name == name) ?? All[2];
}
```

- [ ] **Step 2: Create `StudioConfig.cs`**

```csharp
namespace MyMiniCar.Web.Models;

/// <summary>A user's in-progress custom model design in the single-screen Studio.</summary>
public class StudioConfig
{
    public string SizeKey { get; set; } = "keychain";
    public string MaterialName { get; set; } = "Matte PLA";
    public string TemplateId { get; set; } = "plate";
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;

    public SizeOption Size => Sizes.ByKey(SizeKey);
    public Material Material => Materials.ByName(MaterialName);
    public TextTemplate Template => TextTemplates.ById(TemplateId);

    /// <summary>Engraving is offered only for sizes that allow it (keychain).</summary>
    public bool TextAllowed => Size.AllowsText;

    public decimal Price => Size.BasePrice + Material.Surcharge;

    /// <summary>Colour fed to the 3D preview: the body colour if paintable, else the material's fixed finish.</summary>
    public string EffectiveColor(string bodyColor) =>
        Material.Paintable ? bodyColor : Material.FixedColor;
}
```

- [ ] **Step 3: Delete the old file**

```bash
git rm src/MyMiniCar.Web/Models/KeychainConfig.cs
```

- [ ] **Step 4: Build — expect Convert.razor to FAIL (still references KeychainConfig/Filaments)**

Run: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`
Expected: FAIL with errors in `Convert.razor` about `KeychainConfig` / `_config.FilamentName`. Products.razor and About.razor must NOT error (they use `Filaments`, which still exists). This confirms the move preserved the shared type. Convert.razor is fixed in Task 5.

- [ ] **Step 5: Commit**

```bash
git add src/MyMiniCar.Web/Models/Filament.cs src/MyMiniCar.Web/Models/StudioConfig.cs
git commit -m "refactor: split Filament out, add StudioConfig, drop KeychainConfig"
```

---

## Task 4: xUnit project — price + gating tests

**Files:**
- Create: `tests/MyMiniCar.Tests/MyMiniCar.Tests.csproj`
- Create: `tests/MyMiniCar.Tests/StudioConfigTests.cs`
- Modify: `MyMiniCar.sln`

- [ ] **Step 1: Scaffold the test project and reference the web project**

```bash
dotnet new xunit -n MyMiniCar.Tests -o tests/MyMiniCar.Tests --framework net7.0
dotnet add tests/MyMiniCar.Tests/MyMiniCar.Tests.csproj reference src/MyMiniCar.Web/MyMiniCar.Web.csproj
dotnet sln MyMiniCar.sln add tests/MyMiniCar.Tests/MyMiniCar.Tests.csproj
```

- [ ] **Step 2: Write the failing tests**

Replace the generated `UnitTest1.cs` (delete it) with `StudioConfigTests.cs`:

```bash
rm tests/MyMiniCar.Tests/UnitTest1.cs
```

```csharp
using MyMiniCar.Web.Models;
using Xunit;

namespace MyMiniCar.Tests;

public class StudioConfigTests
{
    [Fact]
    public void Default_is_keychain_matte_pla_at_base_price()
    {
        var cfg = new StudioConfig();
        Assert.Equal("Keychain", cfg.Size.Name);
        Assert.Equal("Matte PLA", cfg.Material.Name);
        Assert.Equal(16.90m, cfg.Price);
    }

    [Theory]
    [InlineData("keychain", "Die-cast Metal", 30.90)]   // 16.90 + 14
    [InlineData("display",  "Carbon Fiber",   53.90)]   // 39.90 + 14
    [InlineData("desk",     "Wood Composite", 30.90)]   // 24.90 + 6
    [InlineData("keychain", "Matte PLA",      16.90)]   // 16.90 + 0
    public void Price_is_size_base_plus_material_surcharge(string size, string material, decimal expected)
    {
        var cfg = new StudioConfig { SizeKey = size, MaterialName = material };
        Assert.Equal(expected, cfg.Price);
    }

    [Fact]
    public void Text_allowed_only_for_keychain()
    {
        Assert.True(new StudioConfig { SizeKey = "keychain" }.TextAllowed);
        Assert.False(new StudioConfig { SizeKey = "desk" }.TextAllowed);
        Assert.False(new StudioConfig { SizeKey = "display" }.TextAllowed);
    }

    [Fact]
    public void EffectiveColor_uses_body_color_for_paintable_else_fixed_finish()
    {
        var paintable = new StudioConfig { MaterialName = "Matte PLA" };
        Assert.Equal("#ff0000", paintable.EffectiveColor("#ff0000"));

        var fixedFinish = new StudioConfig { MaterialName = "Brushed Aluminium" };
        Assert.Equal("#c8ccd2", fixedFinish.EffectiveColor("#ff0000"));
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

Run: `dotnet test tests/MyMiniCar.Tests/MyMiniCar.Tests.csproj`
Expected: PASS, 4 test cases (7 with theory rows). If any fail, fix the model (Task 1–3), not the test.

- [ ] **Step 4: Commit**

```bash
git add tests/MyMiniCar.Tests MyMiniCar.sln
git commit -m "test: cover StudioConfig price, text-gating, effective colour"
```

---

## Task 5: Live preview — `setMaterial` in viewer + GolfViewer params

**Files:**
- Modify: `src/MyMiniCar.Web/wwwroot/js/golf-viewer.js`
- Modify: `src/MyMiniCar.Web/Shared/GolfViewer.razor`

- [ ] **Step 1: Add `setMaterial` to the returned viewer object in `golf-viewer.js`**

In the `return { ... }` block, keep `setColor` and add `setMaterial` next to it:

```js
        // Live recolour of the body paint. Dropping the diffuse map gives a clean,
        // saturated paint colour instead of tinting the baked texture.
        setColor(hex) {
            const mat = state.bodyMaterial;
            if (!mat) return;
            mat.map = null;
            mat.color = new THREE.Color(hex);
            mat.metalness = 0.35;
            mat.roughness = 0.45;
            mat.needsUpdate = true;
        },
        // Apply a finish: effective colour + PBR params. `emissive` makes the body
        // glow in the chosen colour (glow-in-the-dark finish).
        setMaterial({ color, metalness, roughness, emissive }) {
            const mat = state.bodyMaterial;
            if (!mat) return;
            mat.map = null;
            mat.color = new THREE.Color(color);
            mat.metalness = metalness;
            mat.roughness = roughness;
            mat.emissive = emissive ? new THREE.Color(color) : new THREE.Color(0x000000);
            mat.needsUpdate = true;
        },
```

- [ ] **Step 2: Replace `GolfViewer.razor` parameters + apply logic**

Replace the `@code` block's parameters and the color-apply methods so the component takes PBR params and a precomputed effective colour. Full new `@code` block:

```csharp
@code {
    /// <summary>Effective body colour (paintable → body colour, else material fixed finish).</summary>
    [Parameter] public string Color { get; set; } = "#2D6CFF";
    [Parameter] public double Metalness { get; set; } = 0.35;
    [Parameter] public double Roughness { get; set; } = 0.45;
    [Parameter] public bool Emissive { get; set; }
    [Parameter] public string ModelUrl { get; set; } = "models/golf/untitled.gltf";

    private ElementReference _stage;
    private IJSObjectReference? _module;
    private IJSObjectReference? _viewer;
    private string _applied = "";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/golf-viewer.js");
        _viewer = await _module.InvokeAsync<IJSObjectReference>(
            "createViewer", _stage, ModelUrl);
        await ApplyMaterial();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_viewer is not null && Signature != _applied)
            await ApplyMaterial();
    }

    private string Signature => $"{Color}|{Metalness}|{Roughness}|{Emissive}";

    private async Task ApplyMaterial()
    {
        if (_viewer is null) return;
        await _viewer.InvokeVoidAsync("setMaterial", new
        {
            color = Color,
            metalness = Metalness,
            roughness = Roughness,
            emissive = Emissive
        });
        _applied = Signature;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_viewer is not null)
            {
                await _viewer.InvokeVoidAsync("dispose");
                await _viewer.DisposeAsync();
            }
            if (_module is not null)
                await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { /* circuit gone; nothing to clean */ }
    }
}
```

(The markup above `@code` — the `<div class="golf-viewer">` block — stays unchanged.)

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`
Expected: still FAILS on `Convert.razor` only (fixed next task). No new errors in `GolfViewer.razor`.

- [ ] **Step 4: Commit**

```bash
git add src/MyMiniCar.Web/wwwroot/js/golf-viewer.js src/MyMiniCar.Web/Shared/GolfViewer.razor
git commit -m "feat: GolfViewer setMaterial with PBR params"
```

---

## Task 6: Rewrite Convert.razor as a single-screen configurator

**Files:**
- Modify (full rewrite): `src/MyMiniCar.Web/Pages/Convert.razor`

- [ ] **Step 1: Replace the entire file**

```razor
@page "/convert"
@inject CartService CartService

<div class="container py-5">
    <div class="text-center mb-5">
        <span class="eyebrow">The Studio</span>
        <h1 class="mt-3 mb-2">Get your own @HeadingNoun</h1>
        <p class="text-soft mx-auto" style="max-width: 32rem;">
            Pick your car, make it yours — see it spin in 3D, live.
        </p>
    </div>

    <div class="row g-4 g-lg-5">
        <!-- Live preview (sticky) -->
        <div class="col-lg-6">
            <div class="preview-stage">
                <div class="preview-head">
                    <span class="label">Your @HeadingNoun, live</span>
                    <span class="d-inline-flex align-items-center gap-2 text-soft" style="font-size: 0.72rem;">
                        <span class="live-dot"></span> Updating live
                    </span>
                </div>
                <div class="preview-canvas blueprint-soft">
                    <GolfViewer @key="_car.Key"
                                ModelUrl="@_car.GltfPath"
                                Color="@_config.EffectiveColor(_bodyColor)"
                                Metalness="_config.Material.Metalness"
                                Roughness="_config.Material.Roughness"
                                Emissive="_config.Material.Emissive" />
                </div>
                <div class="preview-foot">
                    <span><i class="bi bi-printer me-1"></i>@_car.Name / @_config.Material.Name</span>
                    <span class="mono">@_config.Price.ToString("C2")</span>
                </div>
            </div>
        </div>

        <!-- Controls (all visible) -->
        <div class="col-lg-6">
            <div class="studio-panel">
                <!-- Your car -->
                <label class="field-label">Your car</label>
                <div class="car-model-grid mb-4">
                    @foreach (var car in CarCatalog.All)
                    {
                        var model = car;
                        <button type="button"
                                class="car-model-card @(_car.Key == model.Key ? "selected" : "")"
                                @onclick="() => SelectCar(model)">
                            <span class="car-model-name">@model.Name</span>
                            <span class="car-model-meta">3D preview ready</span>
                        </button>
                    }
                </div>

                <!-- Your colour -->
                <label class="field-label">Your colour</label>
                <div class="golf-swatch-row mb-2">
                    @foreach (var c in _carColors)
                    {
                        var col = c;
                        <button type="button"
                                class="golf-swatch @(_bodyColor == col ? "selected" : "")"
                                style="background:@col"
                                @onclick="() => _bodyColor = col"
                                aria-label="@col"></button>
                    }
                    <label class="golf-swatch golf-swatch-custom" style="background:@_bodyColor">
                        <input type="color" value="@_bodyColor"
                               @oninput="e => _bodyColor = e.Value?.ToString() ?? _bodyColor" />
                    </label>
                </div>
                @if (!_config.Material.Paintable)
                {
                    <p class="text-faint mb-4" style="font-size: 0.74rem;">
                        <i class="bi bi-info-circle"></i> @_config.Material.Name shows its own finish — colour applies to PLA, Resin &amp; Glow.
                    </p>
                }
                else
                {
                    <div class="mb-4"></div>
                }

                <!-- Your size -->
                <label class="field-label">Your size</label>
                <div class="size-grid mb-4">
                    @foreach (var s in Sizes.All)
                    {
                        var size = s;
                        <button type="button"
                                class="size-card @(_config.SizeKey == size.Key ? "selected" : "")"
                                @onclick="() => SelectSize(size)">
                            <span class="size-name">@size.Name</span>
                            <span class="size-dims">@size.Dimensions</span>
                            <span class="size-blurb">@size.Blurb</span>
                            <span class="size-price">@size.BasePrice.ToString("C2")</span>
                        </button>
                    }
                </div>

                <!-- Your finish -->
                <label class="field-label">Your finish</label>
                @foreach (var tier in _tiers)
                {
                    <div class="mat-tier-label">@tier @(Materials.TierSurcharge(tier) > 0 ? $"· +{Materials.TierSurcharge(tier):C0}" : "")</div>
                    <div class="swatch-grid mb-3">
                        @foreach (var m in Materials.All.Where(m => m.Tier == tier))
                        {
                            var mat = m;
                            <div class="swatch @(_config.MaterialName == mat.Name ? "selected" : "")"
                                 @onclick="() => _config.MaterialName = mat.Name">
                                <div class="swatch-chip @mat.Css"></div>
                                <div class="swatch-label">@mat.Name</div>
                            </div>
                        }
                    </div>
                }

                <!-- Add engraving (keychain only, collapsed) -->
                @if (_config.TextAllowed)
                {
                    @if (!_showEngraving)
                    {
                        <button type="button" class="engrave-toggle" @onclick="() => _showEngraving = true">
                            <i class="bi bi-plus-circle"></i> Add your engraving
                        </button>
                    }
                    else
                    {
                        <div class="engrave-panel mt-2">
                            <div class="d-flex justify-content-between align-items-center mb-2">
                                <label class="field-label m-0">Your engraving</label>
                                <button type="button" class="engrave-clear" @onclick="ClearEngraving">
                                    <i class="bi bi-x"></i> Remove
                                </button>
                            </div>
                            <div class="tpl-grid mb-3">
                                @foreach (var t in TextTemplates.All)
                                {
                                    var tpl = t;
                                    <div class="tpl-card @(_config.TemplateId == tpl.Id ? "selected" : "")"
                                         @onclick="() => SelectTemplate(tpl.Id)">
                                        <div class="tpl-name">@tpl.Label</div>
                                        <div class="tpl-desc">@tpl.Description</div>
                                    </div>
                                }
                            </div>
                            <input class="input-c mb-2" maxlength="@_config.Template.MaxLength"
                                   placeholder="@_config.Template.Placeholder"
                                   value="@_config.Line1" @oninput="e => _config.Line1 = e.Value?.ToString() ?? string.Empty" />
                            @if (_config.Template.HasSecondLine)
                            {
                                <input class="input-c" maxlength="@_config.Template.SecondMaxLength"
                                       placeholder="@_config.Template.SecondPlaceholder"
                                       value="@_config.Line2" @oninput="e => _config.Line2 = e.Value?.ToString() ?? string.Empty" />
                            }
                        </div>
                    }
                }
            </div>
        </div>
    </div>
</div>

<!-- Sticky add-to-cart bar -->
<div class="studio-bar">
    <div class="container studio-bar-inner">
        <div class="studio-bar-spec">
            <span class="studio-bar-title">Your @_config.Size.Name</span>
            <span class="studio-bar-sub">@_car.Name · @_config.Material.Name@(EngravingSummary)</span>
        </div>
        <div class="d-flex align-items-center gap-3">
            @if (_added)
            {
                <span class="studio-bar-added"><i class="bi bi-bag-check-fill"></i> It's yours — added!</span>
            }
            <span class="studio-bar-price">@_config.Price.ToString("C2")</span>
            <button class="btn-accent-c" @onclick="AddToBag">
                <i class="bi bi-bag-plus"></i> Get yours
            </button>
        </div>
    </div>
</div>

@code {
    private bool _added;
    private bool _showEngraving;
    private string _bodyColor = "#2D6CFF";
    private CarModel _car = CarCatalog.Default;
    private readonly StudioConfig _config = new();

    private readonly MaterialTier[] _tiers =
    {
        MaterialTier.Standard, MaterialTier.Premium, MaterialTier.Showcase
    };

    private readonly string[] _carColors =
    {
        "#16161a", "#f4f6f9", "#c81f0c", "#1741b8", "#9aa3b0", "#1d7a46", "#e0a020",
    };

    private string HeadingNoun => _config.Size.AllowsText ? "keychain" : "figure";

    private string EngravingSummary
    {
        get
        {
            if (!_config.TextAllowed || string.IsNullOrWhiteSpace(_config.Line1)) return string.Empty;
            return _config.Template.HasSecondLine && !string.IsNullOrWhiteSpace(_config.Line2)
                ? $" · “{_config.Line1} · {_config.Line2}”"
                : $" · “{_config.Line1}”";
        }
    }

    private void SelectCar(CarModel car) { _car = car; _added = false; }

    private void SelectSize(SizeOption size)
    {
        _config.SizeKey = size.Key;
        _added = false;
        if (!_config.TextAllowed) _showEngraving = false;   // figures: hide engraving
    }

    private void SelectTemplate(string id)
    {
        _config.TemplateId = id;
        if (!_config.Template.HasSecondLine) _config.Line2 = string.Empty;
    }

    private void ClearEngraving()
    {
        _showEngraving = false;
        _config.Line1 = string.Empty;
        _config.Line2 = string.Empty;
    }

    private void AddToBag()
    {
        var hasText = _config.TextAllowed && !string.IsNullOrWhiteSpace(_config.Line1);
        var text = hasText
            ? (_config.Template.HasSecondLine && !string.IsNullOrWhiteSpace(_config.Line2)
                ? $"{_config.Line1} · {_config.Line2}"
                : _config.Line1)
            : null;

        var product = new Product
        {
            Id = $"custom-{Guid.NewGuid():N}",
            Name = $"Custom {_car.Name} {_config.Size.Name}",
            Description = $"A 3D-printed {_car.Name} {_config.Size.Name.ToLower()} in {_config.Material.Name}"
                + (text is null ? "." : $", engraved \"{text}\"."),
            Price = _config.Price,
            ImageUrl = string.Empty,
            Category = "Custom",
            DefaultMaterial = _config.Material.Name,
            TileClass = _config.Material.Css,
            Filament = _config.Material.Name,
            CustomText = text,
            Dimensions = _config.Size.Dimensions
        };

        CartService.AddItem(product, _config.Material.Name);
        _added = true;
    }
}
```

- [ ] **Step 2: Build — expect SUCCESS now**

Run: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`
Expected: Build succeeded. No `KeychainConfig`/`Filaments` errors anywhere.

- [ ] **Step 3: Commit**

```bash
git add src/MyMiniCar.Web/Pages/Convert.razor
git commit -m "feat: single-screen Studio configurator with personal copy"
```

---

## Task 7: CSS — single-screen layout, size cards, material swatches, sticky bar

**Files:**
- Modify: `src/MyMiniCar.Web/wwwroot/css/app.css`

- [ ] **Step 1: Append the new Studio styles**

Add this block to the end of the STUDIO section (after the existing `.fil-wood` rule, before the Template chooser is fine — anywhere in the file works). Includes size cards, material tier labels, `mat-*` swatches, engraving toggle, and the sticky bar:

```css
/* ---------- Single-screen Studio additions ---------- */

/* Size cards */
.size-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 0.7rem; }
.size-card {
    appearance: none; background: var(--surface); border: 1.5px solid var(--line);
    border-radius: var(--radius-sm); padding: 0.85rem 0.95rem; text-align: left;
    cursor: pointer; transition: var(--t); display: flex; flex-direction: column; gap: 0.2rem;
}
.size-card:hover { border-color: var(--line-strong); }
.size-card.selected { border-color: var(--primary); box-shadow: var(--ring); }
.size-name { font-weight: 700; color: var(--ink); }
.size-dims { font-family: var(--font-mono); font-size: 0.64rem; letter-spacing: 0.05em; color: var(--ink-faint); text-transform: uppercase; }
.size-blurb { font-size: 0.72rem; color: var(--ink-soft); }
.size-price { font-family: var(--font-display); font-weight: 700; color: var(--ink); margin-top: 0.2rem; }

/* Material tier labels */
.mat-tier-label { font-family: var(--font-mono); font-size: 0.64rem; letter-spacing: 0.08em; text-transform: uppercase; color: var(--ink-soft); margin: 0.4rem 0 0.5rem; }

/* Material swatch finishes */
.mat-pla     { background: linear-gradient(145deg, #6f86ff, #2d6cff); }
.mat-resin   { background: linear-gradient(145deg, #8fa6ff, #2d6cff); box-shadow: inset 0 -2px 6px rgba(255,255,255,0.5), inset 0 2px 4px rgba(0,0,0,0.18); }
.mat-alu     { background: linear-gradient(145deg, #f2f4f7, #aeb4bd 55%, #d7dbe0); }
.mat-wood    { background: linear-gradient(145deg, #d9a86a, #8a5a2b); }
.mat-marble  { background: linear-gradient(145deg, #f4f4f2, #c9ccd2 60%, #8c9098); }
.mat-glow    { background: linear-gradient(145deg, #d7ffe6, #7bf2a8); box-shadow: 0 0 10px rgba(123,242,168,0.7); }
.mat-diecast { background: linear-gradient(145deg, #6b7077, #2b2e33); box-shadow: inset 0 -2px 5px rgba(255,255,255,0.25); }
.mat-carbon  { background: repeating-linear-gradient(45deg, #2a2c32 0 4px, #1a1c20 4px 8px); }

/* Engraving toggle / panel */
.engrave-toggle {
    appearance: none; width: 100%; background: var(--surface-alt); border: 1.5px dashed var(--line-strong);
    border-radius: var(--radius-sm); padding: 0.8rem; color: var(--primary); font-weight: 600;
    cursor: pointer; transition: var(--t);
}
.engrave-toggle:hover { border-color: var(--primary); background: var(--primary-tint); }
.engrave-panel { border: 1px solid var(--line); border-radius: var(--radius-sm); padding: 1rem; background: var(--surface-alt); }
.engrave-clear { appearance: none; background: none; border: none; color: var(--ink-faint); font-size: 0.78rem; cursor: pointer; }
.engrave-clear:hover { color: var(--accent); }

/* Sticky add-to-cart bar */
.studio-bar {
    position: sticky; bottom: 0; z-index: 20;
    background: var(--surface); border-top: 1px solid var(--line);
    box-shadow: 0 -8px 24px rgba(20,28,50,0.08);
}
.studio-bar-inner { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.9rem 1rem; }
.studio-bar-spec { display: flex; flex-direction: column; min-width: 0; }
.studio-bar-title { font-family: var(--font-display); font-weight: 700; color: var(--ink); }
.studio-bar-sub { font-size: 0.78rem; color: var(--ink-soft); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.studio-bar-price { font-family: var(--font-display); font-size: 1.25rem; font-weight: 800; color: var(--ink); }
.studio-bar-added { color: #15803d; font-size: 0.82rem; font-weight: 600; white-space: nowrap; }

@media (max-width: 575px) {
    .studio-bar-sub { display: none; }
    .studio-bar-inner { padding: 0.7rem 0.9rem; }
}
```

- [ ] **Step 2: Remove dead stepper CSS**

Delete the `/* Stepper */` block (the rules `.stepper`, `.stepper-node`, `.stepper-dot`, `.stepper-label`, `.stepper-line`, and the `.stepper-node.active/.done` variants) — no longer referenced. Also remove the `.dropzone` block only if a project-wide search confirms it's unused:

Run: `grep -rn "stepper\|dropzone\|studio-step" src/MyMiniCar.Web --include="*.razor"`
Expected: no matches → safe to delete those CSS blocks. If `dropzone`/`studio-step-title` still match somewhere, leave those rules.

- [ ] **Step 3: Build (CSS is static; just confirm app still builds)**

Run: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/MyMiniCar.Web/wwwroot/css/app.css
git commit -m "style: single-screen Studio layout, size cards, material swatches, sticky bar"
```

---

## Task 8: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test`
Expected: PASS (StudioConfig tests green).

- [ ] **Step 2: Run the app and walk the flow (use the `/run` skill)**

Verify in the browser:
- Page loads with defaults → sticky bar shows **Your Keychain · VW Golf IV · Matte PLA** and **$16.90 · Get yours**. Click Get yours immediately → "It's yours — added!" with zero other interaction.
- Change colour → 3D preview recolours live.
- Select **Brushed Aluminium** → preview switches to silver finish, colour note appears, price → $22.90. Switch back to **Matte PLA** → chosen colour returns.
- Select **Display Figure** → engraving section gone, heading/preview say "figure", price → $39.90.
- Back to **Keychain** → **Add your engraving** → pick template, type → reflected in sticky-bar sub-line → Get yours → cart item shows engraving + correct price.
- Open the cart → custom item name `Custom VW Golf IV Keychain`, material + dimensions present.

- [ ] **Step 3: Final commit (if any verification tweaks were needed)**

```bash
git add -A
git commit -m "fix: studio verification adjustments"
```

(Skip if nothing changed.)

---

## Notes

- **Out of scope, do not touch:** Products/About finish pickers, KeychainPreview, MockProductService — they keep using `Filaments`/`fil-*`.
- **Realism rule:** paintable materials (PLA, Resin, Glow) take the body colour; fixed-finish (Alu, Wood, Marble, Die-cast, Carbon) show their own colour. Centralized in `StudioConfig.EffectiveColor`.
- **No new model assets:** size tiers change price/dimensions/options only; the 3D mesh is the same.
