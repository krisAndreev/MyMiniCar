namespace MyMiniCar.Web.Models;

public enum MaterialTier { Standard, Premium, Showcase }

/// <summary>A realistic print finish selectable in the Studio. PBR fields drive the live 3D preview.</summary>
public class Material
{
    public string Name { get; init; } = string.Empty;
    public MaterialTier Tier { get; init; }

    /// <summary>True = takes the chosen body colour; false = shows its own <see cref="FixedColor"/>.</summary>
    public bool Paintable { get; init; }

    /// <summary>Three.js MeshStandardMaterial metalness (0–1).</summary>
    public double Metalness { get; init; }
    /// <summary>Three.js MeshStandardMaterial roughness (0–1).</summary>
    public double Roughness { get; init; }
    /// <summary>Glow: emit the body colour.</summary>
    public bool Emissive { get; init; }

    /// <summary>Intrinsic colour shown in the preview when not paintable (hex).</summary>
    public string FixedColor { get; init; } = "#9aa3b0";

    /// <summary>CSS swatch class for the picker chip (e.g. "mat-pla").</summary>
    public string Css { get; init; } = string.Empty;

    public decimal Surcharge => Materials.TierSurcharge(Tier);
}

/// <summary>Single source of truth for Studio materials. Matte PLA is the default.</summary>
public static class Materials
{
    public static decimal TierSurcharge(MaterialTier tier) => tier switch
    {
        MaterialTier.Standard => 0m,
        MaterialTier.Premium  => 6m,
        MaterialTier.Showcase => 14m,
        _ => 0m,
    };

    public static readonly IReadOnlyList<Material> All = new[]
    {
        new Material { Name = "Matte PLA",         Tier = MaterialTier.Standard, Paintable = true,  Metalness = 0.00, Roughness = 0.85, Css = "mat-pla" },
        new Material { Name = "Glossy Resin",      Tier = MaterialTier.Standard, Paintable = true,  Metalness = 0.05, Roughness = 0.18, Css = "mat-resin" },
        new Material { Name = "Brushed Aluminium", Tier = MaterialTier.Premium,  Paintable = false, Metalness = 0.90, Roughness = 0.45, FixedColor = "#c8ccd2", Css = "mat-alu" },
        new Material { Name = "Wood Composite",    Tier = MaterialTier.Premium,  Paintable = false, Metalness = 0.00, Roughness = 0.90, FixedColor = "#9a6b3f", Css = "mat-wood" },
        new Material { Name = "Marble Composite",  Tier = MaterialTier.Premium,  Paintable = false, Metalness = 0.05, Roughness = 0.30, FixedColor = "#e8e6e2", Css = "mat-marble" },
        new Material { Name = "Glow in the Dark",  Tier = MaterialTier.Premium,  Paintable = true,  Metalness = 0.00, Roughness = 0.70, Emissive = true, Css = "mat-glow" },
        new Material { Name = "Die-cast Metal",    Tier = MaterialTier.Showcase, Paintable = false, Metalness = 1.00, Roughness = 0.30, FixedColor = "#3a3d42", Css = "mat-diecast" },
        new Material { Name = "Carbon Fiber",      Tier = MaterialTier.Showcase, Paintable = false, Metalness = 0.40, Roughness = 0.50, FixedColor = "#22242a", Css = "mat-carbon" },
    };

    public static Material Default => All[0];

    public static Material ByName(string name) =>
        All.FirstOrDefault(m => m.Name == name) ?? Default;
}
