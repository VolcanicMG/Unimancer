using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unimancer
{
    /// <summary>
    /// Assemble a uGUI GameObject tree from a bridge manifest.json (produced by
    /// the Node-side <c>html_export</c> tool). Each manifest node becomes a real
    /// UI object placed by its design-px rect + inferred anchor:
    ///   sprite -> Image (type=Sliced when a 9-slice border is present),
    ///   icon   -> Image,
    ///   text   -> TextMeshProUGUI (reflection; falls back to UnityEngine.UI.Text),
    ///   group  -> empty RectTransform.
    /// A Canvas (+ EventSystem) is auto-created when none exists. Registered for
    /// Undo so the whole assembly is one undo step.
    ///
    /// Reuses the existing shared helpers: <see cref="GoResolve"/> (parent
    /// resolve + path), <see cref="RectUtil"/>, and <see cref="SpriteImportUtil"/>
    /// (so a freshly-exported PNG can be configured as a Sprite with its
    /// 9-slice border before we assign it to an Image).
    ///
    /// SVG: if a layer's asset is an .svg, Unity only yields a Sprite when
    /// com.unity.vectorgraphics is installed and the importer produced one. We
    /// load whatever Sprite the AssetDatabase has; if none exists yet we leave a
    /// clear note on the node and create the Image without a sprite rather than
    /// failing the whole build.
    /// </summary>
    public class UiBuildFromManifestTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "ui_build_from_manifest";

        /// <inheritdoc />
        public override string Description =>
            "Assemble a uGUI GameObject tree from a bridge manifest.json (sprite->Image[/Sliced], icon->Image, text->TMP, group->RectTransform), " +
            "placing each node by its rect+anchor. Auto-creates Canvas+EventSystem. Returns the built root {instanceID, path}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Build the component described by the manifest.</summary>
        /// <param name="parameters">manifestPath (required, project-relative), parentPath (optional).</param>
        /// <returns>{instanceID, path, notes[]} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var manifestPath = parameters["manifestPath"]?.ToString();
                if (string.IsNullOrEmpty(manifestPath))
                    return new JObject { ["error"] = "manifestPath is required" };

                // Resolve the manifest on disk. Accept a project-relative
                // ("Assets/...") path; map it to an absolute file path.
                var absManifest = ToAbsoluteProjectPath(manifestPath);
                if (!File.Exists(absManifest))
                    return new JObject { ["error"] = $"manifest not found: {manifestPath}" };

                JObject manifest;
                try { manifest = JObject.Parse(File.ReadAllText(absManifest)); }
                catch (Exception pe) { return new JObject { ["error"] = $"manifest parse error: {pe.Message}" }; }

                var nodes = manifest["nodes"] as JArray;
                if (nodes == null || nodes.Count == 0)
                    return new JObject { ["error"] = "manifest has no nodes" };

                // Resolve / ensure a parent. Non-canvas UI needs a Canvas ancestor.
                GameObject parent = null;
                var parentPath = parameters["parentPath"]?.ToString();
                if (!string.IsNullOrEmpty(parentPath))
                {
                    parent = GoResolve.Resolve(parentPath);
                    if (parent == null)
                        return new JObject { ["error"] = $"parent not found: {parentPath}" };
                }
                if (parent == null || parent.GetComponentInParent<Canvas>() == null)
                {
                    var canvas = EnsureCanvas();
                    EnsureEventSystem();
                    if (parent == null) parent = canvas;
                }

                // The directory of the manifest is the project-relative asset base.
                // (html_export wrote each layer's `asset` as a project path already,
                // but we also fall back to resolving relative to this folder.)
                var assetBaseRel = ToProjectRelative(Path.GetDirectoryName(absManifest));

                var notes = new JArray();

                // Build each top-level node (there is normally exactly one — the
                // component root). Multiple are supported for robustness.
                GameObject builtRoot = null;
                foreach (var n in nodes)
                {
                    var go = BuildNode((JObject)n, parent.transform, assetBaseRel, notes);
                    if (builtRoot == null) builtRoot = go;
                }

                if (builtRoot != null)
                    Undo.RegisterCreatedObjectUndo(builtRoot, "Build UI from manifest");

                Selection.activeGameObject = builtRoot;

                return new JObject
                {
                    ["instanceID"] = builtRoot != null ? EntityId.ToULong(builtRoot.GetEntityId()).ToString() : null,
                    ["path"] = builtRoot != null ? GoResolve.Path(builtRoot) : null,
                    ["notes"] = notes,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        // --- recursive build ---------------------------------------------------

        /// <summary>
        /// Build one manifest node and its children, parenting under
        /// <paramref name="parent"/> and placing it by rect + anchor.
        /// </summary>
        /// <param name="node">The manifest node JObject.</param>
        /// <param name="parent">The parent transform to attach under.</param>
        /// <param name="assetBaseRel">Project-relative folder of the manifest (asset fallback base).</param>
        /// <param name="notes">Accumulator for non-fatal warnings.</param>
        /// <returns>The created GameObject.</returns>
        private GameObject BuildNode(JObject node, Transform parent, string assetBaseRel, JArray notes)
        {
            var name = node["name"]?.ToString() ?? "node";
            var type = node["type"]?.ToString() ?? node["kind"]?.ToString() ?? "group";

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            switch (type)
            {
                case "sprite":
                case "icon":
                    BuildImageNode(go, node, type, assetBaseRel, notes);
                    break;
                case "text":
                    BuildTextNode(go, node);
                    break;
                case "group":
                default:
                    // Empty RectTransform container — nothing to add.
                    break;
            }

            // Place the rect using the parent RectTransform's height for the
            // top-left(design) -> bottom-left(Unity) Y flip.
            ApplyRect(rt, node, parent as RectTransform);

            // Recurse.
            if (node["children"] is JArray children)
            {
                foreach (var c in children)
                    BuildNode((JObject)c, go.transform, assetBaseRel, notes);
            }

            EditorUtility.SetDirty(go);
            return go;
        }

        /// <summary>Add an Image to a sprite/icon node, configuring its sprite + 9-slice.</summary>
        private void BuildImageNode(GameObject go, JObject node, string type, string assetBaseRel, JArray notes)
        {
            go.AddComponent<CanvasRenderer>();
            var img = go.AddComponent<Image>();

            var assetRel = ResolveAssetPath(node["asset"]?.ToString(), assetBaseRel);
            var nine = node["nineSlice"] as JArray; // [L,T,R,B] in px, or null

            if (string.IsNullOrEmpty(assetRel))
            {
                notes.Add($"{node["name"]}: no asset path; created empty Image.");
                return;
            }
            if (AssetImporter.GetAtPath(assetRel) == null && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetRel) == null)
            {
                notes.Add($"{node["name"]}: asset not found at '{assetRel}'; created empty Image.");
                return;
            }

            // For PNG sprites with a 9-slice, configure the importer (Sprite +
            // border) via the shared util so Image.type=Sliced works. SVGs are
            // imported by com.unity.vectorgraphics and already yield a Sprite.
            bool isSvg = assetRel.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            if (!isSvg && nine != null && nine.Count == 4)
            {
                var importParams = new JObject
                {
                    ["spriteBorder"] = new JObject
                    {
                        ["left"] = (float)nine[0],
                        ["top"] = (float)nine[1],
                        ["right"] = (float)nine[2],
                        ["bottom"] = (float)nine[3],
                    },
                };
                SpriteImportUtil.Apply(assetRel, importParams);
            }
            else if (!isSvg)
            {
                // Ensure non-9-slice PNGs are at least configured as Sprites.
                SpriteImportUtil.Apply(assetRel, new JObject());
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetRel);
            if (sprite == null)
            {
                // Most common cause for .svg: com.unity.vectorgraphics not
                // installed, so the SVG didn't import as a Sprite.
                notes.Add(isSvg
                    ? $"{node["name"]}: SVG '{assetRel}' did not import as a Sprite — install com.unity.vectorgraphics, then rebuild."
                    : $"{node["name"]}: '{assetRel}' is not a Sprite yet (reimport may be pending).");
                return;
            }

            img.sprite = sprite;
            // Sliced only when this layer carries a viable 9-slice border.
            img.type = (nine != null && nine.Count == 4) ? Image.Type.Sliced : Image.Type.Simple;
        }

        /// <summary>Add a TMP (or UI.Text fallback) to a text node from manifest fields.</summary>
        private void BuildTextNode(GameObject go, JObject node)
        {
            var content = node["text"]?.ToString() ?? "";
            float size = node["size"] != null ? (float)node["size"] : 24f;
            var colorHex = node["color"]?.ToString();
            var align = node["align"]?.ToString() ?? "left";

            var tmpType = ResolveTmpType();
            if (tmpType != null)
            {
                var tmp = go.AddComponent(tmpType);
                try { tmpType.GetProperty("text")?.SetValue(tmp, content); } catch { }
                try { tmpType.GetProperty("fontSize")?.SetValue(tmp, size); } catch { }
                if (TryParseHex(colorHex, out var col))
                    try { tmpType.GetProperty("color")?.SetValue(tmp, col); } catch { }
                // TextAlignmentOptions enum lives in TMPro; set by name reflectively.
                TrySetTmpAlignment(tmpType, tmp, align);
                return;
            }

            // Fallback: legacy UI.Text.
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = Mathf.RoundToInt(size);
            if (TryParseHex(colorHex, out var c)) text.color = c;
            text.alignment =
                align == "center" ? TextAnchor.MiddleCenter :
                align == "right" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // --- rect / anchor placement ------------------------------------------

        /// <summary>
        /// Place a RectTransform from a manifest node's design-px rect + anchor.
        /// Design space is top-left origin; Unity uGUI is bottom-left, so the Y
        /// is flipped against the parent's height. The chosen anchor controls
        /// how the rect maps onto the parent (stretch fills, edges pin, center
        /// centers).
        /// </summary>
        /// <param name="rt">The RectTransform to position.</param>
        /// <param name="node">The manifest node (rect:{x,y,w,h}, anchor).</param>
        /// <param name="parentRt">The parent RectTransform (for height flip + size).</param>
        private void ApplyRect(RectTransform rt, JObject node, RectTransform parentRt)
        {
            var rect = node["rect"] as JObject;
            float x = rect?["x"] != null ? (float)rect["x"] : 0f;
            float y = rect?["y"] != null ? (float)rect["y"] : 0f;
            float w = rect?["w"] != null ? (float)rect["w"] : 100f;
            float h = rect?["h"] != null ? (float)rect["h"] : 100f;
            var anchor = node["anchor"]?.ToString() ?? "center";

            // Parent design height: prefer the live parent rect; fall back to the
            // node's own height so a missing parent still yields sane placement.
            float parentH = parentRt != null ? parentRt.rect.height : h;

            if (anchor == "stretch")
            {
                // Fill the parent: anchors to corners, zero offsets.
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                return;
            }

            rt.sizeDelta = new Vector2(w, h);

            // Map design top-left (x,y) to a Unity anchoredPosition for a
            // top-left-anchored object, then re-anchor to the requested edge.
            // We anchor to top-left as a stable base and compute anchoredPosition
            // so the visual box lands where the design says.
            //   anchoredPos.x = x + pivot.x*w   (pivot centered)
            //   anchoredPos.y = -(y + pivot.y*h) (Y flips downward from top)
            Vector2 pivot;
            Vector2 anchorMin, anchorMax;
            switch (anchor)
            {
                case "left":
                    anchorMin = anchorMax = new Vector2(0f, 0.5f);
                    pivot = new Vector2(0f, 0.5f);
                    rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                    rt.anchoredPosition = new Vector2(x, parentH * 0.5f - (y + h * 0.5f));
                    return;
                case "right":
                    anchorMin = anchorMax = new Vector2(1f, 0.5f);
                    pivot = new Vector2(1f, 0.5f);
                    rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                    // distance from parent right edge
                    float parentW = parentRt != null ? parentRt.rect.width : (x + w);
                    rt.anchoredPosition = new Vector2(-(parentW - (x + w)), parentH * 0.5f - (y + h * 0.5f));
                    return;
                case "top":
                    anchorMin = anchorMax = new Vector2(0.5f, 1f);
                    pivot = new Vector2(0.5f, 1f);
                    rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                    rt.anchoredPosition = new Vector2((x + w * 0.5f) - (parentRt != null ? parentRt.rect.width * 0.5f : x + w * 0.5f), -y);
                    return;
                case "bottom":
                    anchorMin = anchorMax = new Vector2(0.5f, 0f);
                    pivot = new Vector2(0.5f, 0f);
                    rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                    rt.anchoredPosition = new Vector2((x + w * 0.5f) - (parentRt != null ? parentRt.rect.width * 0.5f : x + w * 0.5f), parentH - (y + h));
                    return;
                case "center":
                default:
                    anchorMin = anchorMax = new Vector2(0.5f, 0.5f);
                    pivot = new Vector2(0.5f, 0.5f);
                    rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                    float pcx = parentRt != null ? parentRt.rect.width * 0.5f : x + w * 0.5f;
                    rt.anchoredPosition = new Vector2((x + w * 0.5f) - pcx, parentH * 0.5f - (y + h * 0.5f));
                    return;
            }
        }

        // --- asset path helpers -----------------------------------------------

        /// <summary>Resolve a manifest asset reference to a project-relative path.</summary>
        /// <param name="asset">The manifest `asset` value (usually already 'Assets/...').</param>
        /// <param name="baseRel">Project-relative folder of the manifest, for fallback.</param>
        /// <returns>A project-relative asset path, or null when absent.</returns>
        private static string ResolveAssetPath(string asset, string baseRel)
        {
            if (string.IsNullOrEmpty(asset)) return null;
            asset = asset.Replace('\\', '/');
            if (asset.StartsWith("Assets/")) return asset;
            // Relative file name: resolve under the manifest's folder.
            return string.IsNullOrEmpty(baseRel) ? asset : baseRel.TrimEnd('/') + "/" + asset;
        }

        /// <summary>Map a project-relative or absolute manifest path to an absolute file path.</summary>
        private static string ToAbsoluteProjectPath(string path)
        {
            path = path.Replace('\\', '/');
            if (Path.IsPathRooted(path)) return path;
            // dataPath ends with "/Assets"; project root is its parent.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        /// <summary>Convert an absolute path under the project into a project-relative one.</summary>
        private static string ToProjectRelative(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;
            absPath = absPath.Replace('\\', '/');
            var projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            if (absPath.StartsWith(projectRoot))
                return absPath.Substring(projectRoot.Length).TrimStart('/');
            return absPath;
        }

        // --- color + alignment + TMP helpers ----------------------------------

        /// <summary>Parse a #rrggbb (or #rgb) hex string into a Color. Returns false when unparseable.</summary>
        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(hex)) return false;
            return ColorUtility.TryParseHtmlString(hex, out color);
        }

        /// <summary>Reflectively set a TMP component's alignment from a CSS text-align token.</summary>
        private static void TrySetTmpAlignment(Type tmpType, object tmp, string align)
        {
            try
            {
                var alignProp = tmpType.GetProperty("alignment");
                if (alignProp == null) return;
                var enumType = alignProp.PropertyType; // TMPro.TextAlignmentOptions
                // Map CSS -> TMP enum names: Left/Center/Right (vertical-centered "Midline" variants).
                string enumName =
                    align == "center" ? "Center" :
                    align == "right" ? "Right" : "Left";
                // TMP enum members are like TopLeft/Left/MidlineLeft; "Left"/"Center"/"Right" exist as horizontal-midline values.
                object val;
                try { val = Enum.Parse(enumType, enumName); }
                catch { val = Enum.Parse(enumType, "Midline" + enumName); }
                alignProp.SetValue(tmp, val);
            }
            catch { /* alignment is best-effort */ }
        }

        /// <summary>Find the TextMeshProUGUI type if TMP is loaded; else null.</summary>
        private static Type ResolveTmpType()
        {
            var t = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro")
                    ?? Type.GetType("TMPro.TextMeshProUGUI, TextMeshPro");
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var ft = asm.GetType("TMPro.TextMeshProUGUI"); if (ft != null) return ft; }
                catch { }
            }
            return null;
        }

        // --- canvas / event system (mirrors UiCreateTool) ---------------------

        /// <summary>Ensure a Canvas exists; return the first found or a new one.</summary>
        private static GameObject EnsureCanvas()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindAnyObjectByType<Canvas>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            if (existing != null) return existing.gameObject;
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return go;
        }

        /// <summary>Ensure an EventSystem exists so interactive UI receives input.</summary>
        private static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }
    }
}
