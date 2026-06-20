# `ui/` — uGUI authoring tools

Edit-mode tools for building and inspecting Unity UI (uGUI). Each tool forwards
to a C# tool of the same bridge-command name in
`unity/Editor/Tools/` (auto-discovered by `McpBridge`).

| Tool | Bridge command | C# class | Purpose |
|---|---|---|---|
| `ui_create` | `ui_create` | `UiCreateTool` | Create a uGUI element from an archetype (canvas, panel, image, text, button, rawimage, scrollview, slider, empty-rect) as a proper UI GameObject. Auto-creates Canvas + EventSystem for interactive elements with no Canvas ancestor. |
| `rect_transform_set` | `rect_transform_set` | `RectTransformSetTool` | Set RectTransform layout in one call: a `preset` anchor layout plus any explicit fields (anchors/pivot/position/size/offsets). |
| `ui_dump` | `ui_dump` | `UiDumpTool` | Edit-mode walk of a Canvas (or all scene canvases) returning a tree of name/path/components/full rect — the edit-mode analog of `runtime_ui_list`. |

## Notes

- **TMP optional.** `ui_create` text/button prefer `TextMeshProUGUI` (resolved by
  reflection) and fall back to `UnityEngine.UI.Text` so the package compiles
  without the TextMeshPro package installed.
- The C# side requires the `UnityEngine.UI` assembly reference, added to
  `unity/Editor/Unimancer.Editor.asmdef`.
