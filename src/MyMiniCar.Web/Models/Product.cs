namespace MyMiniCar.Web.Models;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultMaterial { get; set; } = string.Empty;
    public string Dimensions { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }

    /// <summary>CSS filament class used to tint showcase tiles / previews (e.g. "fil-red").</summary>
    public string TileClass { get; set; } = "fil-blue";

    /// <summary>Selected filament for a custom keychain (Studio output). Optional.</summary>
    public string? Filament { get; set; }

    /// <summary>Personalized text engraved on a custom keychain (Studio output). Optional.</summary>
    public string? CustomText { get; set; }
}
