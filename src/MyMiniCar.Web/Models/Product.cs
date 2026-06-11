namespace MyMiniCar.Web.Models;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Bulgarian name/description from the DB (null = fall back to English).</summary>
    public string? NameBg { get; set; }
    public string? DescriptionBg { get; set; }
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultMaterial { get; set; } = string.Empty;
    public string Dimensions { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }

    // TODO #REFACTOR - set real per-product weights (Econt prices by weight)
    public int WeightGrams { get; set; } = 250;

    /// <summary>CSS filament class used to tint showcase tiles / previews (e.g. "fil-red").</summary>
    public string TileClass { get; set; } = "fil-blue";

    /// <summary>Selected filament for a custom keychain (Studio output). Optional.</summary>
    public string? Filament { get; set; }

    /// <summary>Personalized text engraved on a custom keychain (Studio output). Optional.</summary>
    public string? CustomText { get; set; }
}
