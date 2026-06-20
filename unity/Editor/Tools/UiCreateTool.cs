using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unimancer
{
    /// <summary>
    /// Create a uGUI element from an archetype as a proper UI GameObject
    /// (RectTransform + CanvasRenderer where needed). For interactive archetypes
    /// with no Canvas ancestor, a Canvas + EventSystem are auto-created so the
    /// element is actually usable. Registered for Undo.
    ///
    /// TextMeshPro is OPTIONAL: the "text"/"button" archetypes prefer
    /// TextMeshProUGUI via reflection when the TMP assembly is present, and fall
    /// back to UnityEngine.UI.Text so this compiles without TMP installed.
    /// </summary>
    public class UiCreateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "ui_create";

        /// <inheritdoc />
        public override string Description =>
            "Create a uGUI element from an archetype (canvas, panel, image, text, button, rawimage, scrollview, slider, empty-rect) " +
            "as a proper UI GameObject; auto-creates Canvas+EventSystem for interactive elements when needed. Returns {instanceID, path}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Create the UI element described by the archetype.</summary>
        /// <param name="parameters">archetype (required), name, parentPath, anchoredPosition, sizeDelta.</param>
        /// <returns>{instanceID, path} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var archetype = parameters["archetype"]?.ToString();
                if (string.IsNullOrEmpty(archetype))
                    return new JObject { ["error"] = "archetype is required" };

                var name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                    name = archetype;

                // Resolve the requested parent (may be null -> root).
                GameObject parent = null;
                var parentPath = parameters["parentPath"]?.ToString();
                if (!string.IsNullOrEmpty(parentPath))
                {
                    parent = GoResolve.Resolve(parentPath);
                    if (parent == null)
                        return new JObject { ["error"] = $"parent not found: {parentPath}" };
                }

                bool interactive = archetype is "button" or "slider" or "scrollview";

                // The "canvas" archetype builds its own Canvas; everything else
                // needs a Canvas ancestor. Auto-create one (plus EventSystem) when
                // an interactive element would otherwise be orphaned. Non-interactive
                // graphics also need a Canvas ancestor to render, so we ensure one too.
                if (archetype != "canvas")
                {
                    var canvasAncestor = parent != null ? parent.GetComponentInParent<Canvas>() : null;
                    if (canvasAncestor == null)
                    {
                        var canvasGo = EnsureCanvas();
                        // If no explicit parent was given, parent under the canvas we just ensured.
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
                        return new JObject { ["error"] = $"unknown archetype: {archetype}" };
                }

                // One undo step covers the whole subtree.
                Undo.RegisterCreatedObjectUndo(go, "Create UI " + name);

                if (parent != null)
                    go.transform.SetParent(parent.transform, false);

                // Apply optional initial rect tweaks.
                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    if (parameters["anchoredPosition"] is JObject)
                        rt.anchoredPosition = RectUtil.ToVector2(parameters["anchoredPosition"], rt.anchoredPosition);
                    if (parameters["sizeDelta"] is JObject)
                        rt.sizeDelta = RectUtil.ToVector2(parameters["sizeDelta"], rt.sizeDelta);
                }

                Selection.activeGameObject = go;
                EditorUtility.SetDirty(go);

                return new JObject
                {
                    ["instanceID"] = EntityId.ToULong(go.GetEntityId()).ToString(),
                    ["path"] = GoResolve.Path(go),
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        // --- Archetype builders ---

        /// <summary>Create a bare RectTransform GameObject (no graphic).</summary>
        private static GameObject BuildEmptyRect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return go;
        }

        /// <summary>Create a full-screen Canvas with scaler + raycaster.</summary>
        private static GameObject BuildCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
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

        /// <summary>Create a ScrollRect with viewport, content, and a vertical scrollbar.</summary>
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

        // --- Helpers ---

        /// <summary>
        /// Add a text graphic to a GameObject. Prefers TextMeshProUGUI (looked up
        /// reflectively so this compiles without the TMP package) and falls back
        /// to UnityEngine.UI.Text.
        /// </summary>
        private static void AddText(GameObject go, string content)
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
            // The built-in dynamic font; LegacyRuntime.ttf is the Unity 2022+/6.x name.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>Find the TextMeshProUGUI type if the TMP assembly is loaded; else null.</summary>
        private static Type ResolveTmpType()
        {
            // Search every loaded assembly so we don't hard-reference the TMP DLL.
            return Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro")
                   ?? Type.GetType("TMPro.TextMeshProUGUI, TextMeshPro")
                   ?? FindTypeAcrossAssemblies("TMPro.TextMeshProUGUI");
        }

        /// <summary>Scan all loaded assemblies for a type by full name (reflection fallback).</summary>
        private static Type FindTypeAcrossAssemblies(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName); } catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Ensure a Canvas exists in a loaded scene; return the first found or a new one.</summary>
        private static GameObject EnsureCanvas()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<Canvas>();
#endif
            if (existing != null)
                return existing.gameObject;
            var go = BuildCanvas("Canvas");
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return go;
        }

        /// <summary>Ensure an EventSystem exists so interactive UI receives input.</summary>
        private static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        /// <summary>Set a centered RectTransform sizeDelta on a freshly built element.</summary>
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
