using MyMiniCar.Web.Models;
using Xunit;

namespace MyMiniCar.Tests;

public class StudioConfigTests
{
    [Fact]
    public void Default_is_keychain_matte_pla_at_base_price()
    {
        var cfg = new StudioConfig();
        Assert.Equal("Keychain", cfg.Size.Name);
        Assert.Equal("Matte PLA", cfg.Material.Name);
        Assert.Equal(16.90m, cfg.Price);
    }

    [Theory]
    [InlineData("keychain", "Die-cast Metal", 30.90)]   // 16.90 + 14
    [InlineData("display",  "Carbon Fiber",   53.90)]   // 39.90 + 14
    [InlineData("desk",     "Wood Composite", 30.90)]   // 24.90 + 6
    [InlineData("keychain", "Matte PLA",      16.90)]   // 16.90 + 0
    public void Price_is_size_base_plus_material_surcharge(string size, string material, decimal expected)
    {
        var cfg = new StudioConfig { SizeKey = size, MaterialName = material };
        Assert.Equal(expected, cfg.Price);
    }

    [Fact]
    public void Text_allowed_only_for_keychain()
    {
        Assert.True(new StudioConfig { SizeKey = "keychain" }.TextAllowed);
        Assert.False(new StudioConfig { SizeKey = "desk" }.TextAllowed);
        Assert.False(new StudioConfig { SizeKey = "display" }.TextAllowed);
    }

    [Fact]
    public void EffectiveColor_uses_body_color_for_paintable_else_fixed_finish()
    {
        var paintable = new StudioConfig { MaterialName = "Matte PLA" };
        Assert.Equal("#ff0000", paintable.EffectiveColor("#ff0000"));

        var fixedFinish = new StudioConfig { MaterialName = "Brushed Aluminium" };
        Assert.Equal("#c8ccd2", fixedFinish.EffectiveColor("#ff0000"));
    }
}
