namespace MyMiniCar.Api.Models;

/// <summary>Full product row for the admin UI (includes inactive + sort).</summary>
public sealed record AdminProductView(
    string Id, string Name, string Description, string? NameBg, string? DescriptionBg,
    decimal Price, string Category, string? ImageUrl, string? DisplayModelUrl, string? PrintModelUrl,
    string? DefaultMaterial, string? Dimensions, int WeightGrams, string TileClass,
    bool IsFeatured, bool IsActive, int SortOrder);

/// <summary>Create/update payload from the admin UI.</summary>
public sealed record ProductWrite(
    string Id, string Name, string Description, string? NameBg, string? DescriptionBg,
    decimal Price, string Category, string? ImageUrl, string? DisplayModelUrl, string? PrintModelUrl,
    string? DefaultMaterial, string? Dimensions, int WeightGrams, string TileClass,
    bool IsFeatured, bool IsActive, int SortOrder);
