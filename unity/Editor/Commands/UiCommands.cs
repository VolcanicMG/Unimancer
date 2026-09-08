using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unimancer.Commands
{
    /// <summary>
    /// uGUI authoring commands contributed to com.unity.pipeline: create elements
    /// from archetypes, set RectTransform layout, dump a Canvas tree, and assemble
    /// a component from an html_export manifest.
    ///
    /// TextMeshPro is OPTIONAL throughout: text archetypes prefer TextMeshProUGUI
    /// via reflection when the TMP assembly is present, and fall back to
    /// UnityEngine.UI.Text so this compiles without TMP installed.
    /// </summary>
    public static class UiCommands
    {
        // --- ui_create ---------------------------------------------------------

        /// <summary>
        /// Create a uGUI element from an archetype as a proper UI GameObject
        /// (RectTransform + CanvasRenderer where needed). For interactive
        /// archetypes with no Canvas ancestor, a Canvas + EventSystem are
        /// auto-created so the element is actually usable. Registered for Undo.
        /// </summary>
        [CliCommand("ui_create",
            "Create a uGUI element from an archetype as a proper UI GameObject (RectTransform + CanvasRenderer where needed). " +
            "Archetypes: canvas, panel, image, text (TextMeshProUGUI if TMP is present, else UI.Text), button, rawimage, scrollview, slider, empty-rect. " +
            "If an interactive archetype (button/slider/scrollview) is created with no Canvas ancestor, a Canvas + EventSystem are auto-created. " +
            "Returns {instanceID, path}. The action is undoable.",
            Tags = new[] { "ui" })]
        public static Dictionary<string, object> UiCreate(
            [CliArg("archetype", "UI archetype to instantiate (canvas, panel, image, text, button, rawimage, scrollview, slider, empty-rect).", Required = true)] string archetype,
            [CliArg("name", "Name for the new UI GameObject (defaults to the archetype).")] string name = null,
            [CliArg("parentPath", "Hierarchy path or instanceID of an existing parent (ideally a Canvas or another RectTransform).")] string parentPath = null,
            [CliArg("anchoredPosition", "Initial RectTransform anchoredPosition {x,y}.")] Vec2 anchoredPosition = null,
            [CliArg("sizeDelta", "Initial RectTransform sizeDelta {x,y} (width/height when anchors are not stretched).")] Vec2 sizeDelta = null)
        {
            if (string.IsNullOrEmpty(archetype))
                throw new ArgumentException("archetype is required");

            if (string.IsNullOrEmpty(name))
                name = archetype;

            // Resolve the requested parent (may be null -> root).
            GameObject parent = null;
            if (!string.IsNullOrEmpty(parentPath))
            {
                parent = GoResolve.Resolve(parentPath);
                if (parent == null)
                    throw new ArgumentException($"parent not found: {parentPath}");
            }

            bool interactive = archetype is "button" or "slider" or "scrollview";

            // The "canvas" archetype builds its own Canvas; everything else needs a
            // Canvas ancestor to render, so ensure one (plus an EventSystem for
            // interactive elements) when the target would otherwise be orphaned.
            if (archetype != "canvas")
            {
                var canvasAncestor = parent != null ? parent.GetComponentInParent<Canvas>() : null;
                if (canvasAncestor == null)
                {
                    var canvasGo = EnsureCanvas();
                    if (parent == null)
                        parent = canvasGo;
                    if (interactive)
                        EnsureEventSystem();
                }
            }

            GameObject go;
            switch (archetype)
            {
                case "canvas": go = BuildCanvas(name); EnsureEventSystem(); break;
                case "panel": go = BuildImage(name, new Color(1f, 1f, 1f, 0.39f)); break;
                case "image": go = BuildImage(name, Color.white); break;
                case "rawimage": go = BuildRawImage(name); break;
                case "text": go = BuildText(name); break;
                case "button": go = BuildButton(name); break;
                case "slider": go = BuildSlider(name); break;
                case "scrollview": go = BuildScrollView(name); break;
                case "empty-rect": go = BuildEmptyRect(name); break;
                default:
                    throw new ArgumentException($"unknown archetype: {archetype}");
            }

            // One undo step covers the whole subtree.
            Undo.RegisterCreatedObjectUndo(go, "Create UI " + name);

            if (parent != null)
                go.transform.SetParent(parent.transform, false);

            // Apply optional initial rect tweaks.
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (anchoredPosition != null) rt.anchoredPosition = anchoredPosition.ToVector2();
                if (sizeDelta != null) rt.sizeDelta = sizeDelta.ToVector2();
            }

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);

            return new Dictionary<string, object>
            {
                ["instanceID"] = EntityId.ToULong(go.GetEntityId()).ToString(),
                ["path"] = GoResolve.Path(go),
            };
        }

        // --- rect_transform_set ------------------------------------------------

        /// <summary>
        /// Set RectTransform layout in one call: a convenience <c>preset</c> anchor
        /// layout plus any explicit fields. The preset is applied first so explicit
        /// fields can override individual values. Registered for Undo.
        /// </summary>
        [CliCommand("rect_transform_set",
            "Set RectTransform layout on a UI GameObject in one call. Provide a `preset` for a common anchor layout " +
            "(stretch-all, top-bar, bottom-bar, left, right, center, top-left, top-right, bottom-left, bottom-right) and/or any explicit fields. " +
            "A preset is applied first, then explicit fields override it. The change is undoable. Returns the resolved RectTransform layout.",
            Tags = new[] { "ui" })]
        public static Dictionary<string, object> RectTransformSet(
            [CliArg("target", "Hierarchy path or instanceID of the GameObject (must have a RectTransform).", Required = true)] string target,
            [CliArg("preset", "Convenience anchor preset applied before any explicit fields (stretch-all, top-bar, bottom-bar, left, right, center, top-left, top-right, bottom-left, bottom-right).")] string preset = null,
            [CliArg("anchorMin", "Anchor min {x,y} in 0..1.")] Vec2 anchorMin = null,
            [CliArg("anchorMax", "Anchor max {x,y} in 0..1.")] Vec2 anchorMax = null,
            [CliArg("pivot", "Pivot {x,y} in 0..1.")] Vec2 pivot = null,
            [CliArg("anchoredPosition", "Anchored position {x,y}.")] Vec2 anchoredPosition = null,
            [CliArg("sizeDelta", "Size delta {x,y}.")] Vec2 sizeDelta = null,
            [CliArg("offsetMin", "Offset min {x,y} (left/bottom).")] Vec2 offsetMin = null,
            [CliArg("offsetMax", "Offset max {x,y} (right/top).")] Vec2 offsetMax = null)
        {
            var go = GoResolve.Resolve(target);
            if (go == null)
                throw new ArgumentException($"GameObject not found: {target}");

            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                throw new ArgumentException($"GameObject has no RectTransform: {target}");

            Undo.RecordObject(rt, "Set RectTransform");

            // Apply the preset first; explicit fields below can still override.
            if (!string.IsNullOrEmpty(preset) && !ApplyPreset(rt, preset))
                throw new ArgumentException($"unknown preset: {preset}");

            // Order matters: anchors/pivot affect how offsets/positions resolve.
            if (anchorMin != null) rt.anchorMin = anchorMin.ToVector2();
            if (anchorMax != null) rt.anchorMax = anchorMax.ToVector2();
            if (pivot != null) rt.pivot = pivot.ToVector2();
            if (anchoredPosition != null) rt.anchoredPosition = anchoredPosition.ToVector2();
            if (sizeDelta != null) rt.sizeDelta = sizeDelta.ToVector2();
            // offsetMin/offsetMax are set last because they overwrite anchoredPosition/sizeDelta.
            if (offsetMin != null) rt.offsetMin = offsetMin.ToVector2();
            if (offsetMax != null) rt.offsetMax = offsetMax.ToVector2();

            EditorUtility.SetDirty(rt);
            return RectUtil.Describe(rt);
        }

        /// <summary>
        /// Apply a named anchor preset to a RectTransform. Stretch presets also
        /// zero the offsets; anchored presets set a sensible pivot and zero the
        /// anchored position so the element snaps to the chosen edge/corner.
        /// </summary>
        /// <param name="rt">The RectTransform to configure.</param>
        /// <param name="preset">The preset name.</param>
        /// <returns>True if the preset was recognized.</returns>
        private static bool ApplyPreset(RectTransform rt, string preset)
        {
            switch (preset)
            {
                case "stretch-all":
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    return true;
                case "top-bar":
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(0, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(0, rt.offsetMax.y);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "bottom-bar":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.offsetMin = new Vector2(0, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(0, rt.offsetMax.y);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "left":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "right":
                    rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, 0);
                    return true;
                case "center":
                    rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    return true;
                case "top-left": SetCorner(rt, 0, 1); return true;
                case "top-right": SetCorner(rt, 1, 1); return true;
                case "bottom-left": SetCorner(rt, 0, 0); return true;
                case "bottom-right": SetCorner(rt, 1, 0); return true;
                default:
                    return false;
            }
        }

        /// <summary>Anchor a RectTransform to a single corner with a matching pivot.</summary>
        /// <param name="rt">The RectTransform to configure.</param>
        /// <param name="x">Corner x anchor (0 left / 1 right).</param>
        /// <param name="y">Corner y anchor (0 bottom / 1 top).</param>
        private static void SetCorner(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x, y);
            rt.pivot = new Vector2(x, y);
            rt.anchoredPosition = Vector2.zero;
        }

        // --- ui_dump -----------------------------------------------------------

        /// <summary>
        /// EDIT-MODE walk of a Canvas (or every loaded scene's Canvases) returning
        /// a tree of { name, path, components[], rect }.
        /// </summary>
        [CliCommand("ui_dump",
            "EDIT-MODE walk of a Canvas (or all scene Canvases) returning a tree. Each node has: name, path, components[], and a full rect " +
            "{anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, rect:{x,y,width,height}}. " +
            "Pass `target` to dump one canvas; omit it to dump every Canvas in every loaded scene. Returns { canvases:[{ path, tree }] }.",
            Tags = new[] { "ui" })]
        public static Dictionary<string, object> UiDump(
            [CliArg("target", "Hierarchy path or instanceID of a Canvas to dump; omit to dump all scene Canvases.")] string target = null,
            [CliArg("includeInactive", "Include inactive GameObjects in the walk (default true).")] bool includeInactive = true)
        {
            var canvases = new List<object>();

            if (!string.IsNullOrEmpty(target))
            {
                var go = GoResolve.Resolve(target);
                if (go == null)
                    throw new ArgumentException($"target not found: {target}");
                // Resolve up to the nearest Canvas so callers can pass any UI node.
                var canvas = go.GetComponent<Canvas>() ?? go.GetComponentInParent<Canvas>();
                var root = canvas != null ? canvas.gameObject : go;
                canvases.Add(new Dictionary<string, object>
                {
                    ["path"] = GoResolve.Path(root),
                    ["tree"] = BuildNode(root.transform, GoResolve.Path(root), includeInactive),
                });
            }
            else
            {
                // Every Canvas in every loaded scene (top-level canvases only;
                // nested canvases still appear within their parent's tree).
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    Scene scene = EditorSceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var rootGo in scene.GetRootGameObjects())
                    {
                        foreach (var canvas in rootGo.GetComponentsInChildren<Canvas>(true))
                        {
                            // Skip canvases nested under another canvas (rendered within it).
                            if (canvas.transform.parent != null &&
                                canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                                continue;
                            var cgo = canvas.gameObject;
                            canvases.Add(new Dictionary<string, object>
                            {
                                ["scene"] = scene.path,
                                ["path"] = GoResolve.Path(cgo),
                                ["tree"] = BuildNode(cgo.transform, GoResolve.Path(cgo), includeInactive),
                            });
                        }
                    }
                }
            }

            return new Dictionary<string, object> { ["canvases"] = canvases };
        }

        /// <summary>
        /// Recursively serialize a UI node: name, path, components, full rect, and
        /// children.
        /// </summary>
        /// <param name="t">Transform to describe.</param>
        /// <param name="path">Accumulated hierarchy path for this transform.</param>
        /// <param name="includeInactive">Whether to include inactive children.</param>
        /// <returns>A { name, path, active, components[], rect, children[] } map.</returns>
        private static Dictionary<string, object> BuildNode(Transform t, string path, bool includeInactive)
        {
            var node = new Dictionary<string, object>
            {
                ["name"] = t.name,
                ["path"] = path,
                ["active"] = t.gameObject.activeSelf,
                // a missing script serializes as null — skip those
                ["components"] = t.GetComponents<Component>()
                                  .Where(c => c != null)
                                  .Select(c => c.GetType().Name)
                                  .ToList(),
            };

            // Full rect when this is a RectTransform (UI nodes always are).
            if (t is RectTransform rt)
                node["rect"] = RectUtil.Describe(rt);

            var children = new List<object>();
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;
                children.Add(BuildNode(child, path + "/" + child.name, includeInactive));
            }
            node["children"] = children;
            return node;
        }

        // --- ui_build_from_manifest -------------------------------------------

        /// <summary>
        /// Assemble a uGUI GameObject tree from a bridge manifest.json (produced by
        /// the Node-side <c>html_export</c> tool). Registered for Undo so the whole
        /// assembly is one undo step.
        /// </summary>
        [CliCommand("ui_build_from_manifest",
            "Assemble a Unity uGUI GameObject tree from a bridge manifest.json (from html_export). " +
            "Recursively builds sprite->Image (type=Sliced when a 9-slice border is present), icon->Image, " +
            "text->TextMeshProUGUI (falls back to UI.Text), group->empty RectTransform, placing each node by its rect+anchor. " +
            "Auto-creates a Canvas+EventSystem if none exists. SVG assets must already be imported as Sprites " +
            "(needs com.unity.vectorgraphics); otherwise that layer degrades with a clear message. Returns the built root {instanceID, path}.",
            Tags = new[] { "ui" })]
        public static Dictionary<string, object> UiBuildFromManifest(
            [CliArg("manifestPath", "Project-relative path to the manifest.json (e.g. 'Assets/UI/healthbar/manifest.json').", Required = true)] string manifestPath,
            [CliArg("parentPath", "Hierarchy path or instanceID to parent the assembled component under (defaults to a Canvas).")] string parentPath = null)
        {
            if (string.IsNullOrEmpty(manifestPath))
                throw new ArgumentException("manifestPath is required");

            // Resolve the manifest on disk. Accept a project-relative ("Assets/...")
            // path; map it to an absolute file path.
            var absManifest = UiManifestBuilder.ToAbsoluteProjectPath(manifestPath);
            if (!File.Exists(absManifest))
                throw new ArgumentException($"manifest not found: {manifestPath}");

            JObject manifest;
            try { manifest = JObject.Parse(File.ReadAllText(absManifest)); }
            catch (Exception pe) { throw new ArgumentException($"manifest parse error: {pe.Message}"); }

            var nodes = manifest["nodes"] as JArray;
            if (nodes == null || nodes.Count == 0)
                throw new ArgumentException("manifest has no nodes");

            // Resolve / ensure a parent. Non-canvas UI needs a Canvas ancestor.
            GameObject parent = null;
            if (!string.IsNullOrEmpty(parentPath))
            {
                parent = GoResolve.Resolve(parentPath);
                if (parent == null)
                    throw new ArgumentException($"parent not found: {parentPath}");
            }
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                var canvas = EnsureCanvas();
                EnsureEventSystem();
                if (parent == null) parent = canvas;
            }

            // The directory of the manifest is the project-relative asset base.
            var assetBaseRel = UiManifestBuilder.ToProjectRelative(Path.GetDirectoryName(absManifest));

            var notes = new List<string>();

            // Build each top-level node (there is normally exactly one — the
            // component root). Multiple are supported for robustness.
            GameObject builtRoot = null;
            foreach (var n in nodes)
            {
                var go = UiManifestBuilder.BuildNode((JObject)n, parent.transform, assetBaseRel, notes);
                if (builtRoot == null) builtRoot = go;
            }

            if (builtRoot != null)
                Undo.RegisterCreatedObjectUndo(builtRoot, "Build UI from manifest");

            Selection.activeGameObject = builtRoot;

            return new Dictionary<string, object>
            {
                ["instanceID"] = builtRoot != null ? EntityId.ToULong(builtRoot.GetEntityId()).ToString() : null,
                ["path"] = builtRoot != null ? GoResolve.Path(builtRoot) : null,
                ["notes"] = notes,
            };
        }

        // --- ui_click ----------------------------------------------------------

        /// <summary>
        /// Simulate a click on a UI element by dispatching pointer
        /// enter/down/up/click (and submit, for a Selectable) through
        /// <see cref="ExecuteEvents"/>, exercising the real uGUI handler chain —
        /// Button.onClick, Toggle, EventTrigger, any IPointerClickHandler.
        /// </summary>
        [CliCommand("ui_click",
            "Click a UI element to test its interaction, dispatching pointer enter/down/up/click (plus submit on a Selectable) to its uGUI handlers " +
            "(Button onClick, Toggle, EventTrigger, IPointerClickHandler). Meaningful in Play mode, where the handlers actually run. " +
            "Use ui_dump to discover targets. Returns { clicked, target, handlers[] }.",
            Tags = new[] { "ui" })]
        public static Dictionary<string, object> UiClick(
            [CliArg("target", "Hierarchy path or name of the UI element to click.", Required = true)] string target)
        {
            var go = GoResolve.Resolve(target);
            if (go == null)
                throw new ArgumentException($"GameObject not found: {target}");
            if (!go.activeInHierarchy)
                throw new ArgumentException($"GameObject is inactive (can't click): {target}");

            // EventSystem.current may be null outside Play mode; PointerEventData
            // tolerates that, and handlers that need it will simply not respond.
            var ped = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, go.transform.position),
            };

            // Mimic a full click so handlers that track press state behave correctly.
            var handlers = new List<object>();
            if (ExecuteEvents.Execute<IPointerEnterHandler>(go, ped, ExecuteEvents.pointerEnterHandler)) handlers.Add("pointerEnter");
            if (ExecuteEvents.Execute<IPointerDownHandler>(go, ped, ExecuteEvents.pointerDownHandler)) handlers.Add("pointerDown");
            if (ExecuteEvents.Execute<IPointerUpHandler>(go, ped, ExecuteEvents.pointerUpHandler)) handlers.Add("pointerUp");
            if (ExecuteEvents.Execute<IPointerClickHandler>(go, ped, ExecuteEvents.pointerClickHandler)) handlers.Add("pointerClick");
            // Selectables (Button/Toggle/…) also answer submit, which is what keyboard/gamepad drives.
            if (go.GetComponent<Selectable>() != null &&
                ExecuteEvents.Execute<ISubmitHandler>(go, ped, ExecuteEvents.submitHandler)) handlers.Add("submit");

            if (handlers.Count == 0)
                throw new ArgumentException($"No clickable UI handler responded on {target}. Use ui_dump to find clickable elements.");

            return new Dictionary<string, object>
            {
                ["clicked"] = true,
                ["target"] = GoResolve.Path(go),
                ["handlers"] = handlers,
            };
        }

        // --- archetype builders ------------------------------------------------

        /// <summary>Create a bare RectTransform GameObject (no graphic).</summary>
        private static GameObject BuildEmptyRect(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        /// <summary>Create a full-screen Canvas with scaler + raycaster.</summary>
        private static GameObject BuildCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return go;
        }

        /// <summary>Create an Image element with a default 100x100 rect.</summary>
        private static GameObject BuildImage(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.GetComponent<Image>().color = color;
            SetDefaultSize(go, 100, 100);
            return go;
        }

        /// <summary>Create a RawImage element with a default 100x100 rect.</summary>
        private static GameObject BuildRawImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            SetDefaultSize(go, 100, 100);
            return go;
        }

        /// <summary>Create a text element (TMP if available, else UI.Text).</summary>
        private static GameObject BuildText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            AddText(go, name);
            SetDefaultSize(go, 160, 30);
            return go;
        }

        /// <summary>Create a Button with a background Image and a child label.</summary>
        private static GameObject BuildButton(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);
            SetDefaultSize(go, 160, 40);

            // Child label, stretched to fill the button.
            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            Stretch(label.GetComponent<RectTransform>());
            AddText(label, name);
            return go;
        }

        /// <summary>Create a horizontal Slider (background/fill/handle) at a default size.</summary>
        private static GameObject BuildSlider(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            SetDefaultSize(go, 160, 20);
            var slider = go.GetComponent<Slider>();

            // Background.
            var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 1f);

            // Fill area + fill.
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.9f, 1f);

            // Handle slide area + handle.
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 0);
            handle.GetComponent<Image>().color = Color.white;

            // Wire the Slider to its parts.
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return go;
        }

        /// <summary>Create a ScrollRect with viewport and content.</summary>
        private static GameObject BuildScrollView(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.39f);
            SetDefaultSize(go, 200, 200);
            var scroll = go.GetComponent<ScrollRect>();

            // Viewport (masked).
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // Content (top-stretched, grows downward).
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0, 1);
            contentRt.sizeDelta = new Vector2(0, 300);

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            return go;
        }

        // --- shared helpers ----------------------------------------------------

        /// <summary>
        /// Add a text graphic to a GameObject. Prefers TextMeshProUGUI (looked up
        /// reflectively so this compiles without the TMP package) and falls back
        /// to UnityEngine.UI.Text.
        /// </summary>
        /// <param name="go">The GameObject to add the text graphic to.</param>
        /// <param name="content">The initial text content.</param>
        internal static void AddText(GameObject go, string content)
        {
            var tmpType = ResolveTmpType();
            if (tmpType != null)
            {
                var tmp = go.AddComponent(tmpType);
                // Set .text and a sane fontSize via reflection — no compile-time TMP dependency.
                try { tmpType.GetProperty("text")?.SetValue(tmp, content); } catch { }
                try { tmpType.GetProperty("fontSize")?.SetValue(tmp, 24f); } catch { }
                return;
            }

            var text = go.AddComponent<Text>();
            text.text = content;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = DefaultFont();
        }

        /// <summary>The built-in dynamic font; LegacyRuntime.ttf is the Unity 2022+/6.x name.</summary>
        internal static Font DefaultFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        /// <summary>Find the TextMeshProUGUI type if the TMP assembly is loaded; else null.</summary>
        internal static Type ResolveTmpType()
        {
            // Search every loaded assembly so we don't hard-reference the TMP DLL.
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

        /// <summary>Ensure a Canvas exists in a loaded scene; return the first found or a new one.</summary>
        internal static GameObject EnsureCanvas()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (existing != null)
                return existing.gameObject;
            var go = BuildCanvas("Canvas");
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return go;
        }

        /// <summary>Ensure an EventSystem exists so interactive UI receives input.</summary>
        internal static void EnsureEventSystem()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        /// <summary>Set a RectTransform sizeDelta on a freshly built element.</summary>
        private static void SetDefaultSize(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Stretch a RectTransform to fully fill its parent.</summary>
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
