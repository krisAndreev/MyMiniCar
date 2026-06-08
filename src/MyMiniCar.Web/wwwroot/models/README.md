# Car models (3D)

Each car lives in its own folder here: `models/<key>/`.

A model folder holds a self-contained glTF 2.0 export:

```
models/
  golf/
    untitled.gltf        # the glTF (references the .bin + textures by relative name)
    untitled.bin         # binary geometry buffer
    *_diff.png           # diffuse textures referenced by the glTF
```

## Adding a new model

1. Create `models/<key>/` and drop the `.gltf`, its `.bin`, and all textures it
   references (keep the texture filenames the glTF expects — they resolve relative
   to the `.gltf`).
2. Register it in `src/MyMiniCar.Web/Models/CarModel.cs` → `CarCatalog.All`:

   ```csharp
   new CarModel("polo", "VW Polo", "models/polo/scene.gltf"),
   ```

3. The car dropdown (future work) binds to `CarCatalog.All`; selecting an entry
   passes its `GltfPath` to `<GolfViewer ModelUrl="..." />`.

## Notes
- Three.js is vendored locally at `wwwroot/lib/three/` (no CDN dependency).
- The viewer forces materials opaque + front-side so only the exterior shows,
  and recolours the largest mesh (the body) — see `wwwroot/js/golf-viewer.js`.
