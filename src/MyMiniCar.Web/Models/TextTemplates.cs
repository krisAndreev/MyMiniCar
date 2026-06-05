using System.Collections.Generic;
using System.Linq;

namespace MyMiniCar.Web.Models;

/// <summary>A preset for engraving text onto the keychain preview.</summary>
public class TextTemplate
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>Maps to a .kc-text variant class in app.css (e.g. "t-plate").</summary>
    public string CssVariant { get; init; } = string.Empty;

    public string Placeholder { get; init; } = string.Empty;
    public int MaxLength { get; init; } = 16;

    /// <summary>Whether the template uses a second line (e.g. a date).</summary>
    public bool HasSecondLine { get; init; }
    public string SecondPlaceholder { get; init; } = string.Empty;
    public int SecondMaxLength { get; init; } = 12;

    public string SampleLine1 { get; init; } = string.Empty;
    public string SampleLine2 { get; init; } = string.Empty;
}

/// <summary>Single source of truth for the Studio text-template presets.</summary>
public static class TextTemplates
{
    public static readonly IReadOnlyList<TextTemplate> All = new List<TextTemplate>
    {
        new()
        {
            Id = "plate", Label = "License Plate",
            Description = "Boxed number-plate look",
            CssVariant = "t-plate", Placeholder = "MINI 01", MaxLength = 9,
            SampleLine1 = "MINI 01"
        },
        new()
        {
            Id = "name", Label = "Name Tag",
            Description = "Bold name under the car",
            CssVariant = "t-name", Placeholder = "Your name", MaxLength = 14,
            SampleLine1 = "ALEX"
        },
        new()
        {
            Id = "number", Label = "Racing Number",
            Description = "Big race number",
            CssVariant = "t-number", Placeholder = "07", MaxLength = 3,
            SampleLine1 = "07"
        },
        new()
        {
            Id = "anniv", Label = "Anniversary",
            Description = "Name + a date",
            CssVariant = "t-anniv", Placeholder = "Team Riley", MaxLength = 16,
            HasSecondLine = true, SecondPlaceholder = "EST. 2019", SecondMaxLength = 12,
            SampleLine1 = "TEAM RILEY", SampleLine2 = "EST. 2019"
        },
        new()
        {
            Id = "custom", Label = "Custom",
            Description = "Anything you like",
            CssVariant = "t-custom", Placeholder = "Drive safe", MaxLength = 18,
            SampleLine1 = "DRIVE SAFE"
        }
    };

    public static TextTemplate ById(string id) =>
        All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
