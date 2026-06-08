# Car models (3D)

Each car lives in its own folder here: `models/<key>/`.

A model folder holds a self-contained glTF 2.0 export:

```
models/
  golf/
    untitled.gltf
    untitled.bin
    *_diff.png
  audi-a4-2000/
    scene.gltf
    scene.bin
  vw-passat/
    scene.gltf
    scene.bin
    textures/
```

## Current models

- VW Golf IV (`golf`)
- 2000 Audi A4 (`audi-a4-2000`)
- Volkswagen Passat (`vw-passat`)
- Mercedes W124 300CE (`mercedes-w124-300ce`)
- 2005 Skoda Octavia (`skoda-octavia-2005`)

## Adding a new model

1. Create `models/<key>/` and drop the `.gltf`, its `.bin`, and all textures it
   references. Keep the texture filenames the glTF expects.
2. Register it in `src/MyMiniCar.Web/Models/CarModel.cs` -> `CarCatalog.All`:

   ```csharp
   new CarModel("polo", "VW Polo", "models/polo/scene.gltf"),
   ```
