# Studio Redesign — Single-Screen Configurator

**Date:** 2026-06-10
**Branch:** MMC_ProcessModelCreation_Kris
**Status:** Approved (design), pending implementation plan
**Scope:** Replace the multi-step Studio wizard (`Convert.razor`) with a single-screen, conversion-optimized configurator. Add size tiers, replace flat PLA filament swatches with realistic tiered materials that drive the live 3D preview, make engraving optional and keychain-only, and rewrite copy in a personal, second-person voice.

## Goal

The current Studio is a 4-step wizard — **Car → Filament → Text → Review** — with Continue/Back navigation, a stepper, hardcoded size (`55×30×4mm`), and flat PLA colour swatches that only tint a CSS chip.

Two problems: (1) every wizard step is an abandon point — friction kills sale conversion; (2) "materials" aren't realistic and don't show in the 3D preview.

**Redesign for maximum conversion:** collapse the whole wizard into **one screen, zero navigation steps**, with everything visible and a sticky live price + add-to-cart. Add real size options and realistic materials. Make the copy feel personal and owned ("your car", "Get yours").

## Core Principle: One Screen, Zero Steps

No stepper. No Continue/Back. No page transitions. The configurator is a single page:

- **Live 3D preview** on the left (sticky on desktop, top on mobile).
- **Controls** stacked on the right, all visible at once.
- **Sticky price + add-to-cart bar** always in view, updating live on every click.

**Why this converts:** each wizard step is a drop-off point and a delay. A single screen with smart defaults means the product is "complete" the instant the page loads — the user can add to cart in one click, or tweak first. Engraving (the one slow, typing-required step) is optional and collapsed, so it never blocks checkout.

## Out of Scope

- New 3D model assets or geometry changes (size tiers do **not** swap the mesh — same model, different declared dimensions / price / options).
- Adding a 3D ring/plate mesh to the preview (text plate stays a 2D/summary concern).
- Payments / checkout (already implemented separately).

## Layout

```
┌─────────────────────────────┬──────────────────────────────┐
│                             │  Get your own keychain        │
│                             │                              │
│      LIVE 3D PREVIEW        │  Your car      [model chips]  │
│      (sticky)               │  Your colour   [swatches]     │
│                             │  Your size     [3 chips]      │
│   "Your keychain, live"     │  Your finish   [swatch grid]  │
│                             │  + Add engraving  (collapsed) │
│                             │                              │
├─────────────────────────────┴──────────────────────────────┤
│  STICKY BAR:  Your keychain · Matte PLA      $16.90  [Get yours →] │
└──────────────────────────────────────────────────────────────────┘
```

Mobile: preview on top, controls below, sticky add-to-cart bar pinned to the bottom of the viewport.

## Controls (all on one screen)

1. **Your car** — model picker (existing chips) + body colour swatches (existing). Unchanged behaviour.
2. **Your size** — 3 inline chips: Keychain / Desk Figure / Display Figure. Each shows dimensions + price delta.
3. **Your finish** — material swatch grid, grouped by tier (Standard / Premium / Showcase). Drives the live preview via PBR params.
4. **Add engraving** — a collapsed `+ Add engraving` link, **keychain only**. Expands to the existing template + text inputs. Optional; collapsed by default so it never gates the sale. Hidden entirely for figures.

No "review step" — the live preview + sticky bar *are* the review.

## Smart Defaults (product complete on load)

Preselected so add-to-cart works immediately: **VW Golf IV**, **blue body**, **Keychain**, **Matte PLA**, no engraving. Sticky bar reads `$16.90 · Get yours →` from second 0.

## Copy / Voice

Personal, second-person, ownership-forward. The product is *theirs*.

| Element | Copy |
|---|---|
| Page header | **Get your own keychain** |
| Subhead | *Pick your car, make it yours — see it spin in 3D, live.* |
| Preview label | **Your keychain, live** (→ **Your figure, live** when a figure size is selected) |
| Section labels | **Your car**, **Your colour**, **Your size**, **Your finish** |
| Engraving toggle | **+ Add your engraving** |
| Sticky CTA | **Get yours — {price}** |
| Post-add toast | **It's yours — added to cart!** |

"Keychain" in the header swaps to "model" / "figure" wording when a figure size is selected (e.g. preview label and CTA context). Keep it light — header can stay "Get your own model" if dynamic swapping is awkward; finalize during implementation.

## Size Tiers

A size sets the base price and whether text/ring applies. Same 3D mesh for all.

| Key | Name | Dimensions | Base price | Ring | Engraving |
|------|--------------|------------|-----------|------|-----------|
| `keychain` | Keychain | 55 × 30 × 4 mm | $16.90 | yes | yes |
| `desk` | Desk Figure | ~70 mm | $24.90 | no | no |
| `display` | Display Figure | ~110 mm | $39.90 | no | no |

- `keychain` is the default.
- Only `keychain` shows the engraving toggle.

## Materials

Replaces `Filament`. Each material carries PBR parameters so the choice renders in the live preview, a tier that sets its surcharge, and a CSS swatch class for the picker chip.

**Surcharge by tier:** Standard +$0, Premium +$6, Showcase +$14.

| Material | Tier | +$ | Paintable | Metalness | Roughness | Notes |
|----------------------|----------|----|-----------|-----------|-----------|-------|
| Matte PLA | Standard | 0 | yes | 0.00 | 0.85 | takes chosen colour, flat |
| Glossy Resin | Standard | 0 | yes | 0.05 | 0.18 | takes colour, high-detail sheen |
| Brushed Aluminium | Premium | 6 | no | 0.90 | 0.45 | fixed silver finish |
| Wood Composite | Premium | 6 | no | 0.00 | 0.90 | fixed warm wood tone |
| Marble Composite | Premium | 6 | no | 0.05 | 0.30 | fixed light stone tone |
| Glow in the Dark | Premium | 6 | yes | 0.00 | 0.70 | takes colour + emissive |
| Die-cast Metal | Showcase | 14 | no | 1.00 | 0.30 | fixed dark chrome/steel |
| Carbon Fiber | Showcase | 14 | no | 0.40 | 0.50 | fixed dark weave tone |

**Paintable vs fixed-finish (realism rule):**
- **Paintable** (Matte PLA, Glossy Resin, Glow) apply the chosen body colour to the preview.
- **Fixed-finish** (Aluminium, Wood, Marble, Die-cast, Carbon) override the chosen paint with their own intrinsic colour — aluminium isn't "Racing Red". The chosen colour is retained in config but not shown while a fixed-finish material is selected; switching back to a paintable material restores it.
- **Glow** is paintable and additionally sets an emissive tint matching the colour.

Fixed-finish base colours (approximate, finalize in implementation): Aluminium `#c8ccd2`, Wood `#9a6b3f`, Marble `#e8e6e2`, Die-cast `#3a3d42`, Carbon `#22242a`.

## Pricing

```
Price = Size.BasePrice + Material.Surcharge
```

Engraving is free. Examples: Keychain+Matte PLA = $16.90; Keychain+Die-cast = $30.90; Display+Carbon = $53.90; Desk+Wood = $30.90.

## Data Model Changes

Split the single `Models/KeychainConfig.cs` (currently holds Filament + Filaments + KeychainConfig) into focused files:

### `Models/SizeOption.cs` (new)
```csharp
public record SizeOption(
    string Key, string Name, string Dimensions,
    decimal BasePrice, bool HasRing, bool AllowsText, string Blurb);

public static class Sizes
{
    public static readonly IReadOnlyList<SizeOption> All = /* keychain, desk, display */;
    public static SizeOption Default => All[0];          // keychain
    public static SizeOption ByKey(string key) => ...;
}
```

### `Models/Material.cs` (new — reworks `Filament`)
```csharp
public enum MaterialTier { Standard, Premium, Showcase }

public class Material
{
    public string Name { get; init; }
    public MaterialTier Tier { get; init; }
    public decimal Surcharge { get; init; }   // derived from tier
    public bool Paintable { get; init; }
    public double Metalness { get; init; }
    public double Roughness { get; init; }
    public bool Emissive { get; init; }        // glow
    public string FixedColor { get; init; }     // used when !Paintable
    public string Css { get; init; }            // picker swatch class
}

public static class Materials
{
    public static readonly IReadOnlyList<Material> All = /* 8 materials above */;
    public static Material ByName(string name) => ...;   // default Matte PLA
}
```

### `Models/StudioConfig.cs` (new — reworks `KeychainConfig`)
```csharp
public class StudioConfig
{
    public string SizeKey { get; set; } = "keychain";
    public string MaterialName { get; set; } = "Matte PLA";
    public string TemplateId { get; set; } = "plate";
    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";

    public SizeOption Size => Sizes.ByKey(SizeKey);
    public Material Material => Materials.ByName(MaterialName);
    public TextTemplate Template => TextTemplates.ById(TemplateId);

    public bool TextAllowed => Size.AllowsText;
    public decimal Price => Size.BasePrice + Material.Surcharge;
}
```

Old `Filaments.BasePrice` constant moves into `Sizes` (per-size base). `Filament`/`Filaments`/`KeychainConfig` removed; references migrated.

## Live Preview (3D) Changes

`wwwroot/js/golf-viewer.js`: add a `setMaterial` method on the viewer object:
```js
setMaterial({ color, metalness, roughness, emissive }) {
    const mat = state.bodyMaterial;
    if (!mat) return;
    mat.map = null;
    mat.color = new THREE.Color(color);
    mat.metalness = metalness;
    mat.roughness = roughness;
    mat.emissive = emissive ? new THREE.Color(color) : new THREE.Color(0x000000);
    mat.needsUpdate = true;
}
```
Keep `setColor` for back-compat, or have it delegate to `setMaterial` with current PBR defaults.

`Shared/GolfViewer.razor`: add parameters `Metalness`, `Roughness`, `Emissive`, and an effective-colour input. Effective colour = body colour when material is paintable, else material's `FixedColor`. Re-invoke `setMaterial` whenever colour or any PBR param changes (extend `OnParametersSetAsync` change-tracking beyond just `Color`).

## Convert.razor Changes

- Remove the stepper, `_step` state, Next/Back, and all step-gated `@if` blocks.
- Single layout: sticky preview column + controls column + sticky add-to-cart bar.
- Render all controls at once: Your car (model + colour), Your size (size chips), Your finish (tiered material grid), Add engraving (collapsed toggle, keychain only).
- Engraving section: local `bool _showEngraving` toggled by the `+ Add your engraving` link; collapses the existing template + line inputs. Auto-hidden when `!config.TextAllowed`.
- Sticky bar: live `config.Price`, short spec line, **Get yours — {price}** button → `AddToBag`.
- Preview footer + label reflect size + material; label swaps keychain/figure wording.
- `AddToBag`: product `Name = $"Custom {car.Name} {size.Name}"`, `Dimensions = size.Dimensions`, material stored in product material field, `CustomText` only when keychain + non-empty. Toast: **It's yours — added to cart!**

## CSS Changes (`wwwroot/css/app.css`)

- Single-screen configurator layout: sticky preview, controls column, sticky bottom add-to-cart bar (desktop + mobile responsive).
- Size-tier chips.
- Material tier grouping + swatch textures for new finishes (brushed alu, wood, marble, die-cast, carbon) alongside existing `fil-*` (or new `mat-*` classes).
- Collapsible engraving section.
- Remove now-unused stepper CSS (`stepper`, `stepper-node`, etc.) if not referenced elsewhere.

## Migration / Ripple Checks (resolve during planning)

- `MockProductService` / `Products.razor` / showcase tiles referencing `Filaments`, `fil-*`, or `KeychainConfig` — migrate to `Materials` / new classes.
- `Product.Filament` / `Product.DefaultMaterial` / `TileClass` usages — keep field names or rename consistently; keep cart display working.
- `KeychainPreview.razor` (2D text-plate preview) — confirm where used; keep functional for keychain engraving.

## Testing

No test project exists (Blazor Server UI). Verification plan:

1. `dotnet build` — clean compile after the model split + migrations.
2. Manual walk via `/run`:
   - Page loads with defaults → sticky bar shows `$16.90 · Get yours`, add-to-cart works with zero other clicks.
   - Change size to Desk/Display → engraving toggle disappears, price updates, preview label says "figure".
   - Paintable material shows chosen colour; fixed-finish (Die-cast) overrides paint; switching back to Matte PLA restores colour.
   - Expand engraving (keychain) → type → reflected in cart item; collapsed/empty → no engraving on product.
   - Price math per tier matches the table.
3. Optional (flag to user): small xUnit project covering `StudioConfig.Price` + text-gating — pure functions, cheap. Not included by default.

## Open Decisions (none blocking)

- Header keychain/figure dynamic wording vs static "your own model" — finalize visually.
- Exact fixed-finish hex tones + CSS swatch textures.
- `setColor` kept vs folded into `setMaterial` — implementation detail.
