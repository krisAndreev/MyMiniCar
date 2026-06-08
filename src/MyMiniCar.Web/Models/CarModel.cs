namespace MyMiniCar.Web.Models;

/// <summary>
/// A selectable 3D car model. <see cref="GltfPath"/> is relative to wwwroot.
/// </summary>
/// <param name="Key">Stable id used in URLs / selection state.</param>
/// <param name="Name">Display name for the (future) car dropdown.</param>
/// <param name="GltfPath">Path to the model's .gltf, relative to wwwroot.</param>
public record CarModel(string Key, string Name, string GltfPath);

/// <summary>
/// Catalog of available car models. Drop a new model under
/// <c>wwwroot/models/&lt;key&gt;/</c> and register it here; the dropdown
/// (future work) binds to <see cref="All"/>.
/// </summary>
public static class CarCatalog
{
    public static readonly IReadOnlyList<CarModel> All = new[]
    {
        new CarModel("golf", "VW Golf IV", "models/golf/untitled.gltf"),
    };

    public static CarModel Default => All[0];

    public static CarModel? ByKey(string key) =>
        All.FirstOrDefault(m => m.Key == key);
}
