namespace MyMiniCar.Web.Models;

/// <summary>
/// A selectable 3D car model. <see cref="GltfPath"/> is relative to wwwroot.
/// </summary>
/// <param name="Key">Stable id used in URLs / selection state.</param>
/// <param name="Name">Display name for the car picker.</param>
/// <param name="GltfPath">Path to the model's .gltf, relative to wwwroot.</param>
public record CarModel(string Key, string Name, string GltfPath);

/// <summary>
/// Catalog of available car models. Drop a new model under
/// <c>wwwroot/models/&lt;key&gt;/</c> and register it here.
/// </summary>
public static class CarCatalog
{
    public static readonly IReadOnlyList<CarModel> All = new[]
    {
        new CarModel("golf", "VW Golf IV", "models/golf/untitled.gltf"),
        new CarModel("audi-a4-2000", "2000 Audi A4", "models/audi-a4-2000/scene.gltf"),
        new CarModel("vw-passat", "Volkswagen Passat", "models/vw-passat/scene.gltf"),
        new CarModel("mercedes-w124-300ce", "Mercedes W124 300CE", "models/mercedes-w124-300ce/scene.gltf"),
        new CarModel("skoda-octavia-2005", "2005 Skoda Octavia", "models/skoda-octavia-2005/scene.gltf"),
    };

    public static CarModel Default => All[0];

    public static CarModel? ByKey(string key) =>
        All.FirstOrDefault(m => m.Key == key);
}
