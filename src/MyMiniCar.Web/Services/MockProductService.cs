using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyMiniCar.Web.Models;

namespace MyMiniCar.Web.Services;

public class MockProductService : IProductService
{
    private readonly List<Product> _products = new()
    {
        new Product
        {
            Id = "custom-car-keychain",
            Name = "Custom Car Keychain",
            Description = "The one that started it all. Upload a photo of your car and we print it as a chunky, car-shaped keyring tag in your choice of PLA filament — finished with your name, plate, or race number.",
            Price = 16.90m,
            Category = "Custom",
            DefaultMaterial = "Racing Red",
            TileClass = "fil-red",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = true
        },
        new Product
        {
            Id = "twin-pack",
            Name = "Twin Pack Keychains",
            Description = "Two matching keychains of the same ride — one for you, one for the glovebox (or a fellow enthusiast). Mix filaments if you like.",
            Price = 28.00m,
            Category = "Bundles",
            DefaultMaterial = "Racing Blue",
            TileClass = "fil-blue",
            Dimensions = "2 × 55mm × 30mm",
            IsFeatured = true
        },
        new Product
        {
            Id = "glow-edition",
            Name = "Glow-in-the-Dark Edition",
            Description = "Charged by daylight, glowing green at night. Find your keys in the dark and turn heads at every meet.",
            Price = 21.90m,
            Category = "Custom",
            DefaultMaterial = "Glow in the Dark",
            TileClass = "fil-glow",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = true
        },
        new Product
        {
            Id = "marble-premium",
            Name = "Marble PLA Premium",
            Description = "Each tag prints with a unique swirled marble pattern — no two are alike. A premium finish for a standout ride.",
            Price = 22.90m,
            Category = "Premium",
            DefaultMaterial = "Marble PLA",
            TileClass = "fil-marble",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = false
        },
        new Product
        {
            Id = "wood-fill",
            Name = "Wood-Fill Rustic Keychain",
            Description = "Real wood fibres in the filament give a warm, sandable, genuinely wooden feel — a classic look for classic cars.",
            Price = 23.90m,
            Category = "Premium",
            DefaultMaterial = "Wood Fill",
            TileClass = "fil-wood",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = false
        },
        new Product
        {
            Id = "led-keyring",
            Name = "LED Light-Up Keyring",
            Description = "Add-on hardware: a press-button LED keyring that clips to any MyMiniCar tag and lights up your custom car on demand.",
            Price = 12.00m,
            Category = "Accessories",
            DefaultMaterial = "Midnight Black",
            TileClass = "fil-black",
            Dimensions = "Keyring · 28mm",
            IsFeatured = false
        },
        new Product
        {
            Id = "display-stand",
            Name = "Magnetic Display Stand",
            Description = "A little printed plinth so your keychain doubles as a desk or shelf piece when it's off the keys.",
            Price = 9.50m,
            Category = "Accessories",
            DefaultMaterial = "Silver Steel",
            TileClass = "fil-silver",
            Dimensions = "40mm × 40mm × 18mm",
            IsFeatured = false
        },
        new Product
        {
            Id = "hardware-pack",
            Name = "Keyring Hardware Pack",
            Description = "Spare split-rings, lobster clips and ball-chains so you can rig your keychains exactly how you want.",
            Price = 5.00m,
            Category = "Accessories",
            DefaultMaterial = "Midnight Black",
            TileClass = "fil-black",
            Dimensions = "Mixed hardware ×10",
            IsFeatured = false
        }
    };

    public Task<IEnumerable<Product>> GetProductsAsync()
        => Task.FromResult<IEnumerable<Product>>(_products);

    public Task<IEnumerable<Product>> GetFeaturedProductsAsync()
        => Task.FromResult<IEnumerable<Product>>(_products.Where(p => p.IsFeatured));

    public Task<Product?> GetProductByIdAsync(string id)
        => Task.FromResult(_products.FirstOrDefault(p => p.Id == id));
}
