using System.Collections.Generic;
using System.Linq;

namespace MyMiniCar.Web.Models;

/// <summary>A selectable PLA filament finish for a keychain.</summary>
public class Filament
{
    public string Name { get; init; } = string.Empty;
    /// <summary>CSS class for the swatch chip + plate tint (e.g. "fil-red").</summary>
    public string Css { get; init; } = string.Empty;
    /// <summary>Added cost over the base keychain price.</summary>
    public decimal Surcharge { get; init; }
    public bool IsPremium { get; init; }
}

public static class Filaments
{
    public const decimal BasePrice = 16.90m;

    public static readonly IReadOnlyList<Filament> All = new List<Filament>
    {
        new() { Name = "Midnight Black", Css = "fil-black" },
        new() { Name = "Pure White", Css = "fil-white" },
        new() { Name = "Racing Red", Css = "fil-red" },
        new() { Name = "Racing Blue", Css = "fil-blue" },
        new() { Name = "Silver Steel", Css = "fil-silver" },
        new() { Name = "Glow in the Dark", Css = "fil-glow", Surcharge = 4m, IsPremium = true },
        new() { Name = "Marble PLA", Css = "fil-marble", Surcharge = 5m, IsPremium = true },
        new() { Name = "Wood Fill", Css = "fil-wood", Surcharge = 5m, IsPremium = true }
    };

    public static Filament ByName(string name) =>
        All.FirstOrDefault(f => f.Name == name) ?? All[2];
}

/// <summary>A user's in-progress keychain design in the Studio.</summary>
public class KeychainConfig
{
    public string? ImageDataUrl { get; set; }
    public string FilamentName { get; set; } = "Racing Red";
    public string TemplateId { get; set; } = "plate";
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;

    public Filament Filament => Filaments.ByName(FilamentName);
    public TextTemplate Template => TextTemplates.ById(TemplateId);
    public bool HasImage => !string.IsNullOrEmpty(ImageDataUrl);

    public decimal Price => Filaments.BasePrice + Filament.Surcharge;
}
