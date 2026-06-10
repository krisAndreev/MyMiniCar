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
            Id = "golf-keychain",
            Name = "VW Golf IV Keychain",
            Description = "The hot-hatch icon, shrunk to a pocket-sized keyring tag. Printed to order in your choice of PLA filament — add your name, plate or race number in the Studio.",
            Price = 16.90m,
            ImageUrl = "images/cars/golf.png",
            Category = "Hatchback",
            DefaultMaterial = "Racing Red",
            TileClass = "fil-red",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = true
        },
        new Product
        {
            Id = "audi-a4-keychain",
            Name = "Audi A4 (B6) Keychain",
            Description = "The understated 2000s saloon, faithfully miniaturised. A clean, premium silhouette that prints beautifully in any finish.",
            Price = 16.90m,
            ImageUrl = "images/cars/audi-a4-2000.png",
            Category = "Saloon",
            DefaultMaterial = "Silver Steel",
            TileClass = "fil-silver",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = true
        },
        new Product
        {
            Id = "passat-keychain",
            Name = "VW Passat B5 Keychain",
            Description = "The do-it-all family Volkswagen as a chunky keyring tag. Looks sharp in deep solid colours.",
            Price = 16.90m,
            ImageUrl = "images/cars/vw-passat.png",
            Category = "Saloon",
            DefaultMaterial = "Racing Blue",
            TileClass = "fil-blue",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = false
        },
        new Product
        {
            Id = "mercedes-w124-keychain",
            Name = "Mercedes W124 300CE Keychain",
            Description = "The bulletproof modern classic. A coupé profile with the kind of presence that earns a premium finish.",
            Price = 18.90m,
            ImageUrl = "images/cars/mercedes-w124-300ce.png",
            Category = "Classic",
            DefaultMaterial = "Marble PLA",
            TileClass = "fil-marble",
            Dimensions = "55mm × 30mm × 4mm",
            IsFeatured = true
        },
        new Product
        {
            Id = "skoda-octavia-keychain",
            Name = "Skoda Octavia Keychain",
            Description = "The dependable daily, miniaturised. A crisp three-box shape that prints clean in every filament.",
            Price = 16.90m,
            ImageUrl = "images/cars/skoda-octavia-2005.png",
            Category = "Saloon",
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
