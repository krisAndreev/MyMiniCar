namespace MyMiniCar.Web.Models;

/// <summary>A fixed product size/format the user can pick in the Studio.</summary>
/// <param name="Key">Stable id used in selection state.</param>
/// <param name="Name">Display name (e.g. "Keychain").</param>
/// <param name="Dimensions">Human-readable dimensions for the spec line.</param>
/// <param name="BasePrice">Base price before any material surcharge.</param>
/// <param name="HasRing">Whether the format includes a keyring.</param>
/// <param name="AllowsText">Whether engraving is offered (keychain only).</param>
/// <param name="Blurb">Short one-line description for the size chip.</param>
public record SizeOption(
    string Key, string Name, string Dimensions,
    decimal BasePrice, bool HasRing, bool AllowsText, string Blurb);

/// <summary>Catalog of selectable sizes. The keychain is the default.</summary>
public static class Sizes
{
    public static readonly IReadOnlyList<SizeOption> All = new[]
    {
        new SizeOption("keychain", "Keychain", "55 × 30 × 4 mm", 16.90m, true,  true,
            "Pocket-sized, with a keyring + text plate"),
        new SizeOption("desk",     "Desk Figure", "≈ 70 mm",      24.90m, false, false,
            "A standalone mini for your desk"),
        new SizeOption("display",  "Display Figure", "≈ 110 mm",  39.90m, false, false,
            "Bigger, with the finest detail"),
    };

    public static SizeOption Default => All[0];

    public static SizeOption ByKey(string key) =>
        All.FirstOrDefault(s => s.Key == key) ?? Default;
}
