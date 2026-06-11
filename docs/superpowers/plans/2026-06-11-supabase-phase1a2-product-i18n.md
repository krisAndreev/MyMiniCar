# Phase 1A.2 — Product Text i18n (BG/EN) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Checkbox steps.
> **Execution rule:** ONE task per turn, commit each. Resume from first unchecked box.

**Goal:** Store product name/description in both Bulgarian and English in the DB (Option A — per-language columns), make the DB the source of truth, and remove the hard-coded BG translation dictionaries from the client.

**Architecture:** `products` gains `name_bg` + `description_bg` (existing `name`/`description` are the English/default). The Api returns BOTH languages on `ProductDto`; the browser keeps its instant client-side language toggle, with `LanguageService.ProductName/ProductDescription` selecting between the English and Bulgarian fields on the `Product` model (replacing the hard-coded dictionaries).

**Tech Stack:** Supabase Postgres, Npgsql, Blazor WASM.

**Source of BG strings:** the existing `_productNamesBg` / `_productDescriptionsBg` dictionaries in `src/MyMiniCar.Web/Services/LanguageService.cs` (added in branch `MMC_AddLabeling_Vasko`).

---

## Task 1: Migration + seed (name_bg, description_bg)

**Files:**
- Create: `supabase/migrations/0004_product_i18n.sql`
- Modify: `supabase/seed.sql`

- [x] **Step 1: Write the migration (adds columns + backfills BG)**
- [x] **Step 2: Apply it to the live DB via psql**
- [x] **Step 3: Add the BG columns to seed.sql for fresh installs**
- [x] **Step 4: Commit**

## Task 2: Api DTO + repository return both languages

**Files:**
- Modify: `src/MyMiniCar.Api/Models/ProductDto.cs`
- Modify: `src/MyMiniCar.Api/Data/ProductRepository.cs`

- [x] **Step 1: Add NameBg/DescriptionBg to ProductDto**
- [x] **Step 2: Select the bg columns in the repository**
- [x] **Step 3: Build + smoke-test endpoint shows bg fields**
- [x] **Step 4: Commit**

## Task 3: Web model + LanguageService

**Files:**
- Modify: `src/MyMiniCar.Web/Models/Product.cs`
- Modify: `src/MyMiniCar.Web/Services/LanguageService.cs`

- [x] **Step 1: Add NameBg/DescriptionBg to Product**
- [x] **Step 2: Rewrite ProductName/ProductDescription to use model fields; drop the dicts**
- [x] **Step 3: Build**
- [x] **Step 4: Commit**

## Task 4: End-to-end verify

- [x] **Step 1: API returns localized fields for all 8 products**
- [x] **Step 2: Commit plan complete**

## Self-Review
- Option A per-language columns ✓. Both languages returned → instant client toggle preserved.
- `custom-` product fallback in `ProductName` retained (Studio products aren't in the DB).
