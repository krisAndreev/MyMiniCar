# AURA | Luxury 3D Rendered Gifts Online Shop

A premium, minimalist C# Blazor WebAssembly application for rendering and purchasing high-end 3D physical and digital artifacts. 

The project structure is designed to be **clean**, **cross-platform**, and **PC-independent**, allowing multiple developers to collaborate seamlessly using **Rider** or **Visual Studio 2022**.

---

## 🏗️ Architecture & Structure

The repository uses a standard Solution-Project setup:

```text
MyMiniCar/
├── MyMiniCar.sln                # Main solution file for VS 2022 / Rider
├── .gitignore                   # Ignores bin, obj, .vs, .idea, and user configurations
├── README.md                    # Developer documentation
└── src/
    └── MyMiniCar.Web/           # Blazor WebAssembly Standalone Client
        ├── Models/              # Domain models (Product, CartItem)
        ├── Services/            # Decoupled Business Logic & State Managers
        │   ├── IProductService.cs   # Interface for Catalog data (Supabase ready)
        │   ├── MockProductService.cs# Mock catalog with premium pre-rendered gifts
        │   └── CartService.cs       # Shared reactive Shopping Bag state container
        ├── Pages/               # Blazor Razor Pages (Index, Products, Convert, About)
        ├── Shared/              # Shared Layout components (MainLayout, NavMenu)
        ├── wwwroot/             # Static Assets (Images, Custom CSS, Index HTML)
        │   ├── css/
        │   │   └── app.css      # Core Luxury Design System (Obsidian & Gold)
        │   └── images/          # Pre-rendered 8k product asset images
        ├── Program.cs           # Dependency Injection & Startup Config
        └── App.razor            # Global Client Routing
```

---

## 🎨 Luxury Design System

We avoid default layouts and standard CSS libraries (like Tailwind or default Bootstrap rules) in favor of a curated, high-end design:
- **Color Palette**: Obsidian Dark (`#0A0A0C`), Velvet Charcoal (`#141418`), and Champagne Gold (`#DFBA73`) accents.
- **Typography**: Google Fonts loaded via CDN—*Cormorant Garamond* (luxurious serif headers) & *Outfit* (sleek modern body text).
- **Glassmorphism**: Headers and dropdown elements utilize blur filters (`backdrop-filter: blur(20px)`).
- **Micro-Animations**: Custom hover transformations, smooth card slide-ins, and a custom CSS 3D rotating wireframe pedestal representing physical mesh loading.

---

## 🔬 Studio 3D Converter (Simulated Neural Sintering)

Located in `/convert`, the **Studio 3D** workspace lets users simulate converting an image into a physical 3D sculpture:
1. **Upload**: Select or drag-and-drop a photograph, pick detail thresholds, and choose luxury material finishes (Obsidian, Marble, Gold, Emerald Glass).
2. **Process**: Live terminal logs capture depth calculations, point triangulation, and mesh watertight checking in real-time.
3. **Orbit Viewport**: An interactive glowing wireframe spins on a 3D pedestal to mimic the generated model.
4. **Exhibition bag**: The synthesized artifact can be added directly to the Shopping Bag.

---

## 💳 Payments (Stripe Checkout)

Card payments are handled by **Stripe Checkout**. Because a Blazor WASM app runs entirely in
the browser, it can never hold the Stripe **secret key** — so a tiny backend API
(`src/MyMiniCar.Api`) creates the Checkout Session and the browser is only ever handed a
redirect URL.

```text
Browser (WASM)  →  MyMiniCar.Api  →  Stripe  →  hosted card page  →  /order-confirmed
```

**Flow:** Cart → `Checkout.razor` (shipping form + order summary) → API creates a Stripe
session → redirect to Stripe's hosted card page → on success Stripe returns the user to
`/order-confirmed?session_id=…`, which verifies the payment via the API and clears the cart.
Free shipping over $40 is applied automatically (a flat $4.90 below that).

### Configuring keys

The publishable key is harmless in the client; the **secret key never lives in source control**
— it's stored in .NET user-secrets:

```bash
cd src/MyMiniCar.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
```

The WASM client reads the API location from `src/MyMiniCar.Web/wwwroot/appsettings.json`
(`ApiBaseUrl`, default `http://localhost:5230`). CORS origins are configured in the API's
`appsettings.json`.

### Running with payments

Run **both** projects (two terminals):

```bash
# Terminal 1 — payments API (http://localhost:5230)
dotnet run --project src/MyMiniCar.Api/MyMiniCar.Api.csproj

# Terminal 2 — storefront (http://localhost:5229)
dotnet run --project src/MyMiniCar.Web/MyMiniCar.Web.csproj
```

In **test mode**, complete checkout with card `4242 4242 4242 4242`, any future expiry, any CVC.

### Testing cards 

4000 0000 0000 0002	Generic decline ("Your card was declined")
4000 0000 0000 9995	Decline — insufficient funds
4000 0000 0000 9987	Decline — lost card
4000 0000 0000 0069	Expired card
4000 0000 0000 0127	Incorrect CVC
4000 0000 0000 0119	Processing error
4000 0027 6000 3184	Forces a 3D Secure authentication popup

### Going live later

1. Activate the live account in the Stripe dashboard (identity + IBAN).
2. Swap the user-secret to the **live** secret key (`sk_live_...`).
3. Deploy `MyMiniCar.Api` somewhere with HTTPS and point `ApiBaseUrl` at it.

---

## 🚀 Future Integrations

- **Database (Supabase)**: Replace `MockProductService` in `Program.cs` with a client implementing `IProductService` communicating with your Supabase database endpoints.
- **Hosting (Vercel)**: Since this is a Blazor WebAssembly standalone app, building the project yields static files under `bin/Release/net7.0/publish/wwwroot/` which can be published directly to Vercel (e.g. using the Vercel CLI or Git integration).

---

## 💻 Running the Project

### Prerequisites
- .NET 7.0 SDK

### CLI
Run the following in the root folder:
```bash
dotnet restore
dotnet run --project src/MyMiniCar.Web/MyMiniCar.Web.csproj
```
The application will launch at:
- `http://localhost:5229`
- `https://localhost:7109`

### IDE Setup
- **Visual Studio 2022 / Rider**: Simply open `MyMiniCar.sln` at the root and click **Run**. Launch profiles are defined inside `/src/MyMiniCar.Web/Properties/launchSettings.json` with relative hosts, making it fully portable.
