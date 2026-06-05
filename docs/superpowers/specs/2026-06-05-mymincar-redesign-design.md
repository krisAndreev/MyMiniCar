# MyMiniCar — UI Redesign & Car-Keychain Studio Design

**Date:** 2026-06-05
**Status:** Approved pending user review
**Owner:** kristiyanand2004@gmail.com

---

## 1. Goal

Pivot the existing Blazor WebAssembly app from "AURA — luxury 3D *rendered* abstract sculptures" to **MyMiniCar — 3D-printed car keychains**.

Core promise: **a user uploads a photo of their car, previews it as a 3D-printed keychain, personalizes it with preset text templates, and buys it.**

The app must become **clean, modern, responsive**, and visually oriented around **3D printing + automotive** — not luxury gallery aesthetics.

## 2. Decisions (locked with user)

| Topic | Decision |
|---|---|
| Preview generation | **Live composite mock** — read real upload in-browser, composite into a keychain render via CSS layering driven by Blazor state. No backend, no ML. |
| Visual direction | **Bright maker studio** — light UI, filament-color accents, blueprint grid, geometric sans. |
| Brand name | **MyMiniCar** (replaces AURA everywhere). |
| Materials | **Real PLA filaments** (black, white, racing red, blue, silver, glow, marble, wood-fill). |
| Plate shape | **Car silhouette** — uploaded photo masked into a car-shaped outline (SVG `clip-path`). |
| Cart/checkout | **Keep mock cart** — reskin drawer; checkout stays a placeholder. |

## 3. Scope

### In scope
- New design system in `wwwroot/css/app.css` (light maker-studio tokens, type, components).
- Brand rename AURA → MyMiniCar (nav, footer, README/UI copy, `index.html` fonts/title).
- **Studio** (`/convert`, relabeled "Studio"): real file upload, live car-silhouette keychain preview, filament picker, preset text templates, add-to-cart.
- **Catalog** (`/products`): car-keychain showcases + accessories via rewritten `MockProductService`; reskinned cards/modal/filters.
- **Home** (`/`): new hero + "how it works" strip + featured showcases, all reskinned.
- **How it works** (`/about`): repurpose About into a print-process/materials page.
- Reskinned cart drawer (in `MainLayout`).
- User-flow documentation (this spec, §8).

### Out of scope (flagged future work)
- Real photo→3D mesh generation (needs ML service + backend).
- Real payment/checkout backend (Supabase, Stripe).
- Canvas "download preview as image" export — *optional stretch*, see §6.4.

## 4. Design System (`app.css`)

Replace the obsidian/gold luxury tokens with a bright maker-studio system.

**Color tokens**
- `--bg`: `#F7F8FA` (off-white canvas)
- `--surface`: `#FFFFFF` (cards)
- `--surface-alt`: `#EEF1F6` (insets, blueprint base)
- `--ink`: `#16181D` (primary text)
- `--ink-soft`: `#5A6473` (secondary text)
- `--line`: `#E2E6EE` (borders)
- `--primary`: `#2D6CFF` (racing blue — links, focus, primary actions)
- `--primary-ink`: `#FFFFFF`
- `--accent`: `#FF4D3D` (racing red — CTAs / highlights)
- Filament swatch colors defined as utility classes (see §6.3).

**Typography**
- Headings: **Space Grotesk** (geometric, technical).
- Body: **Inter**.
- Loaded via Google Fonts in `index.html`; remove Cormorant Garamond / Outfit.

**Shape & depth**
- Cards/inputs: `border-radius: 16px` (replace current sharp `border-radius: 0`).
- Soft layered shadows (`0 1px 2px`, `0 8px 24px rgba(20,24,40,.08)`).
- Subtle **blueprint grid** background utility (faint `--line` grid) for hero/studio panels.

**Components to restyle:** buttons (`.btn-primary`, `.btn-ghost` replace `.btn-gold`/`.btn-outline-gold`), cards, nav, footer, modal, cart drawer, stepper, dropzone, option pills, filament swatches.

> Old class names (`.btn-gold`, `.luxury-card`, `.text-gold`, etc.) are used across pages. Strategy: **rename to new semantic classes and update all `.razor` references** rather than leaving dead luxury names. Keep a thin compatibility note only if a class is reused widely.

## 5. Architecture (unchanged shell)

Stays Blazor WASM standalone, no backend. DI, routing, `IProductService`/`CartService` unchanged in contract.

```
Pages/
  Index.razor        # Home — rewritten copy + reskin
  Products.razor     # Catalog — reskin + car data
  Convert.razor      # Studio — real rewrite (core)
  About.razor        # How it works — rewritten
Shared/
  MainLayout.razor   # Cart drawer reskin, footer/brand rename
  NavMenu.razor      # Brand + links rename
Models/
  Product.cs         # + optional fields (see §6.5)
Services/
  MockProductService.cs   # car-keychain catalog
wwwroot/
  css/app.css        # new design system
  index.html         # fonts, title, brand
  images/            # need car/keychain showcase images (placeholder strategy §7)
```

### New units introduced

- **`KeychainPreview.razor`** (component): given an image data-URL + style options (filament, text-template config), renders the live car-silhouette keychain. One clear job: render preview. Inputs via `[Parameter]`. No business logic. Reused by Studio (and potentially the cart/modal later).
- **`TextTemplates.cs`** (static catalog in `Models/` or `Services/`): defines the preset text templates (id, label, font, size, color, position anchor, casing, sample text). One source of truth, consumed by Studio.
- **`KeychainConfig.cs`** (model): the user's in-progress design (image data-URL, filament, selected template id, custom text). Built in Studio, converted to a `Product` on add-to-cart.

## 6. Studio — detailed design (the core)

Route stays `/convert`; nav label and headings say **"Studio"**.

Two-column responsive layout (stacks on mobile): **left = controls (stepper)**, **right = sticky live `KeychainPreview`**.

### 6.1 Real upload
- Replace raw `<input type="file">` + faked `HandleFileSelected` with Blazor **`<InputFile OnChange=...>`**.
- On change: validate `ContentType` starts with `image/` and size ≤ 10 MB; read via `file.OpenReadStream(maxAllowedSize: 10MB)` → bytes → base64 → `data:` URL stored in `KeychainConfig.ImageDataUrl`.
- Errors surface inline (wrong type, too big) — no exceptions bubble.
- Drag-and-drop kept (styled dropzone); clicking opens the file picker (wire the label/`InputFile` properly so it actually triggers — current code's `TriggerFileInput` is a no-op).

### 6.2 Live car-silhouette preview (`KeychainPreview`)
- An **SVG car silhouette** used as a `clip-path` (`<clipPath>`) masking the uploaded photo into a car shape.
- Layer stack (CSS-positioned, updates reactively from Blazor params):
  1. Keychain body: car-shaped plate tinted by selected filament (filament texture overlay = subtle layer-line gradient to read as "printed").
  2. Keyring ring hardware (small circle + hole) at a fixed anchor.
  3. The user's photo, clipped to the car silhouette, sitting on the plate.
  4. Text overlay(s) per the active template (position/font/color from template config + user's text).
- Empty state (no image): show silhouette outline + "Upload a photo of your car" prompt.
- Fully responsive: preview scales with container; uses relative units / `viewBox`.

### 6.3 Filament picker
- Swatch grid (PLA): Black, White, Racing Red, Racing Blue, Silver, Glow-in-Dark, Marble, Wood-Fill.
- Each swatch = CSS gradient chip; selecting re-tints the keychain body + (where appropriate) text default color.

### 6.4 Text templates (presets)
Preset templates, each editable. User picks a template → edits text → live update.

| Template | Style | Default position | Casing |
|---|---|---|---|
| License Plate | mono, boxed plate look | bottom band | UPPER |
| Name Tag | bold sans | under car | Title |
| Racing Number | extra-bold, large | center overlay | as-typed |
| Anniversary | name + date, two lines | bottom | Title |
| Custom | free text | bottom (movable later) | as-typed |

- Defined in `TextTemplates.cs`; Studio renders a template chooser + a text input (or two for Anniversary). Character limits per template to avoid overflow.
- *(Optional stretch §6.4-export):* a "Download preview" button using a small JS-interop module to rasterize the preview node to PNG. Separate, non-blocking; cut if time-constrained.

### 6.5 Add to cart
- Build a `Product` from `KeychainConfig`: name e.g. `"Custom Car Keychain"`, price from filament tier, `ImageUrl` = the composited preview is not persistable cheaply → use the uploaded image data-URL (or a showcase fallback) for the cart thumbnail, material = filament, plus a short description summarizing text + filament.
- `Product.cs` gains optional fields: `Filament` (string), `CustomText` (string) — additive, nullable/default, no breaking change.
- Calls existing `CartService.AddItem`.

### 6.6 Stepper
Reframe steps to: **Upload → Style → Personalize → Review**. Live preview visible at every step. Replace "Analyze/Sinter" simulation entirely.

## 7. Images / assets

No real product photos exist (current images are luxury renders). Strategy:
- Studio preview is generated live — needs **no** product image.
- Catalog showcases + home featured need imagery. Use **CSS/SVG-generated placeholder keychain tiles** (car silhouette on filament-tinted card) rather than shipping fake photos, so the app looks intentional without sourcing assets. Document where to drop real product photos later.

## 8. User flows (analyzed)

### Flow A — Primary: customize & buy (hero path)
1. Home → "Make your keychain" CTA → **Studio**.
2. **Upload** car photo (drag/drop or browse). Validation feedback if bad file.
3. **Style:** pick filament → preview re-tints.
4. **Personalize:** pick text template → type text → live preview.
5. **Review:** see final keychain + price → **Add to cart**.
6. Cart drawer → (mock) checkout.

### Flow B — Browse catalog
1. Home/nav → **Shop** → filter by category/sort.
2. Card → modal (showcase + filament select) → Add to cart, **or** "Customize yours" → Studio (Flow A).

### Flow C — Learn
1. Nav → **How it works** → print process, materials, sizing, shipping → CTA into Studio.

### Edge cases handled
- No image uploaded → preview empty state; "Add to cart" disabled until image + (optional) text present.
- Wrong file type / oversized → inline error, upload rejected.
- Empty/over-long text → template char limits; empty text just hides the text layer.
- Mobile → columns stack, preview moves above controls or sticky-top.

## 9. Testing / verification

- Build passes: `dotnet build src/MyMiniCar.Web/MyMiniCar.Web.csproj`.
- Manual run (`dotnet run`) + verify in browser:
  - Real image upload renders in preview (not a hardcoded filename).
  - Filament change re-tints; template change updates text live.
  - Add-to-cart populates drawer with the custom item.
  - Responsive at mobile/tablet/desktop widths.
- No Razor/CSS references to removed luxury classes remain (grep for `btn-gold`, `luxury-`, `text-gold`, `Cormorant`, `AURA`).

## 10. Risks / notes

- **Clip-path car silhouette + arbitrary photo:** photo aspect ratios vary; use `object-fit: cover` inside the clip so any photo fills the silhouette cleanly.
- **Preview-as-cart-thumbnail:** compositing to a real image for the cart needs canvas; for this pass the cart thumb uses the raw uploaded photo (acceptable; note for later).
- **Not a git repo** (`git: false`): spec is saved to disk; commit deferred until the project is initialized (will confirm with user).
- Implementation will use the **frontend-design** skill for polish.
