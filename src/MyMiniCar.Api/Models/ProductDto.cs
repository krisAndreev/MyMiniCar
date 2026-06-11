namespace MyMiniCar.Api.Models;

/// <summary>Product shape returned to the browser. Property names match
/// MyMiniCar.Web.Models.Product so the client deserializes directly.</summary>
public sealed record ProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    string Category,
    string DefaultMaterial,
    string Dimensions,
    bool IsFeatured,
    int WeightGrams,
    string TileClass);
