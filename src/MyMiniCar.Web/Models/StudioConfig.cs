namespace MyMiniCar.Web.Models;

/// <summary>A user's in-progress custom model design in the single-screen Studio.</summary>
public class StudioConfig
{
    public string SizeKey { get; set; } = "keychain";
    public string MaterialName { get; set; } = "Matte PLA";
    public string TemplateId { get; set; } = "plate";
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;

    public SizeOption Size => Sizes.ByKey(SizeKey);
    public Material Material => Materials.ByName(MaterialName);
    public TextTemplate Template => TextTemplates.ById(TemplateId);

    /// <summary>Engraving is offered only for sizes that allow it (keychain).</summary>
    public bool TextAllowed => Size.AllowsText;

    public decimal Price => Size.BasePrice + Material.Surcharge;

    /// <summary>Colour fed to the 3D preview: the body colour if paintable, else the material's fixed finish.</summary>
    public string EffectiveColor(string bodyColor) =>
        Material.Paintable ? bodyColor : Material.FixedColor;
}
