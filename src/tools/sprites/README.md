# `sprites/` — sprite import & procedural generation

Edit-mode tools for turning images into Unity sprites. Each tool forwards to a
C# tool of the same bridge-command name in `unity/Editor/Tools/`.

| Tool | Bridge command | C# class | Purpose |
|---|---|---|---|
| `sprite_import` | `sprite_import` | `SpriteImportTool` | Configure an existing image's `TextureImporter` as a Single sprite (pixelsPerUnit, pivot, 9-slice border, filterMode) and reimport. |
| `sprite_generate` | `sprite_generate` | `SpriteGenerateTool` | Procedurally render a `Texture2D` (solid / gradient[linear,radial] / checker / border-frame), encode to PNG, write into `Assets/`, then apply the same sprite import config. |

## Notes

- Colors are `{r,g,b,a}` with channels in **0..255** (alpha optional, default 255).
- `sprite_generate` writes PNG bytes via `Texture2D.EncodeToPNG()` and imports
  with `AssetDatabase.ImportAsset`; the 9-slice border and pivot are applied by
  the shared `SpriteImportUtil` (also used by `sprite_import`).
- Importer logic is borrowed from CoPlay `ManageTexture.cs` / `TextureOps.cs`.
