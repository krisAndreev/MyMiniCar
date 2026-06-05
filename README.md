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
