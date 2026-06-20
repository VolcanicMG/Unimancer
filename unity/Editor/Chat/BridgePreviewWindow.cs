using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// A dedicated popout that shows the per-component, per-LAYER crops produced by the
    /// `html_preview` MCP tool, so the user can review/approve how each widget
    /// decomposes WITHOUT flooding the chat with images. For each component it shows the
    /// FULL widget plus every separated layer (the frame on its own, each peeled
    /// sub-sprite, and each icon) — so nothing is baked into the frame. The chat's
    /// Preview button asks the agent to run `html_preview` with `inline:false` + an
    /// `outDir`; this window watches that dir for `preview-index.json`, loads the crops,
    /// and labels each name ABOVE its image.
    /// </summary>
    public class BridgePreviewWindow : EditorWindow
    {
        // One separated layer (frame / sub-sprite / icon) within a component.
        private struct Layer
        {
            public string Name;
            public string Kind;   // "sprite" | "icon"
            public int[] Border;  // [L,T,R,B] design px, or null (no 9-slice)
            public Texture2D Tex;
        }

        // One loaded component: metadata + its full crop + per-layer crops.
        private struct Item
        {
            public string Name;
            public string Archetype;
            public int Texts;
            public int Icons;
            public Texture2D FullTex;
            public List<Layer> Layers;
        }

        private string _dir;          // Windows dir the crops are written to
        private string _source;       // source HTML path (for the header)
        private readonly List<Item> _items = new List<Item>();
        private Vector2 _scroll;
        private bool _loaded;
        private double _lastPoll;
        private string _status = "Waiting for the preview to be generated…";

        /// <summary>
        /// Open (or focus) the preview popout and point it at the output directory the
        /// agent will write the crops + index into.
        /// </summary>
        /// <param name="dir">Directory that will receive the crops + 'preview-index.json'.</param>
        /// <param name="source">Source HTML path, shown in the header.</param>
        public static void Open(string dir, string source)
        {
            var w = GetWindow<BridgePreviewWindow>(false, "HTML Preview", true);
            w.minSize = new Vector2(400f, 480f);
            w.Point(dir, source);
            w.Show();
            w.Focus();
        }

        /// <summary>Re-point the window at a (possibly new) output dir and reset its state.</summary>
        /// <param name="dir">Output directory to watch.</param>
        /// <param name="source">Source HTML path for the header.</param>
        private void Point(string dir, string source)
        {
            ClearTextures();
            _dir = dir;
            _source = source;
            _items.Clear();
            _loaded = false;
            _lastPoll = 0;
            _status = "Waiting for the preview to be generated…";
            SweepOldTempDirs(dir); // clear previews left by prior runs/crashes
        }

        private void OnEnable() { EditorApplication.update += Poll; }
        private void OnDisable() { EditorApplication.update -= Poll; ClearTextures(); }

        // Window closed for good → remove its temp crops. (Domain reload fires
        // OnDisable, not OnDestroy, so a recompile won't delete an open preview.)
        private void OnDestroy() { DeleteDirSafe(_dir); }

        /// <summary>Poll the output dir (~1s) for the index until the crops are loaded.</summary>
        private void Poll()
        {
            if (_loaded || string.IsNullOrEmpty(_dir)) return;
            if (EditorApplication.timeSinceStartup - _lastPoll < 1.0) return;
            _lastPoll = EditorApplication.timeSinceStartup;
            TryLoad();
        }

        /// <summary>Load preview-index.json + every crop texture; no-op until the index exists.</summary>
        private void TryLoad()
        {
            try
            {
                var idxPath = Path.Combine(_dir, "preview-index.json");
                if (!File.Exists(idxPath)) return;
                var data = JsonUtility.FromJson<PreviewIndex>(File.ReadAllText(idxPath));
                if (data == null || data.components == null) { _status = "preview-index.json was empty or invalid."; _loaded = true; Repaint(); return; }

                ClearTextures();
                _items.Clear();
                foreach (var c in data.components)
                {
                    var layers = new List<Layer>();
                    if (c.layers != null)
                        foreach (var l in c.layers)
                            layers.Add(new Layer { Name = l.name, Kind = l.kind, Border = l.border, Tex = LoadPng(l.file) });
                    _items.Add(new Item
                    {
                        Name = c.name,
                        Archetype = c.archetype,
                        Texts = c.texts,
                        Icons = c.icons,
                        FullTex = LoadPng(c.full),
                        Layers = layers,
                    });
                }
                _loaded = true;
                Repaint();
            }
            catch (Exception e)
            {
                _status = "Failed to load preview: " + e.Message;
                _loaded = true;
                Repaint();
            }
        }

        /// <summary>Load a PNG (relative to the output dir) into a throwaway texture, or null.</summary>
        /// <param name="file">File name relative to <see cref="_dir"/> (may be null/empty).</param>
        /// <returns>The loaded texture, or null if missing/unreadable.</returns>
        private Texture2D LoadPng(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            var path = Path.Combine(_dir, file);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
            DestroyImmediate(tex);
            return null;
        }

        /// <summary>Destroy all loaded textures (on reload/close/repoint) to avoid leaks.</summary>
        private void ClearTextures()
        {
            foreach (var it in _items)
            {
                if (it.FullTex != null) DestroyImmediate(it.FullTex);
                if (it.Layers != null)
                    foreach (var l in it.Layers)
                        if (l.Tex != null) DestroyImmediate(l.Tex);
            }
        }

        /// <summary>
        /// Delete every stale "unimancer-preview-*" temp dir except <paramref name="keep"/>,
        /// so previews from prior runs or crashes don't accumulate. Best-effort.
        /// </summary>
        /// <param name="keep">The current output dir to preserve.</param>
        private static void SweepOldTempDirs(string keep)
        {
            try
            {
                var keepFull = string.IsNullOrEmpty(keep) ? "" : Path.GetFullPath(keep);
                foreach (var d in Directory.GetDirectories(Path.GetTempPath(), "unimancer-preview-*"))
                    if (!string.Equals(Path.GetFullPath(d), keepFull, StringComparison.OrdinalIgnoreCase))
                        DeleteDirSafe(d);
            }
            catch { /* best-effort */ }
        }

        /// <summary>Delete a preview temp dir if it looks like ours and exists. Best-effort.</summary>
        /// <param name="dir">Directory to delete.</param>
        private static void DeleteDirSafe(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return;
                if (dir.IndexOf("unimancer-preview-", StringComparison.OrdinalIgnoreCase) < 0) return; // safety guard
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { /* locked or already gone */ }
        }

        private void OnGUI()
        {
            DrawHeader();
            if (!_loaded)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(_status, EditorStyles.centeredGreyMiniLabel);
                Repaint(); // keep ticking until Poll() loads it
                return;
            }
            if (_items.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("No components were found in the preview.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            var metaStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            var subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.7f, 1f) } };
            foreach (var it in _items)
            {
                EditorGUILayout.LabelField(it.Name, nameStyle);
                EditorGUILayout.LabelField($"{it.Archetype}  ·  {it.Layers.Count} layer(s)  ·  {it.Texts} text, {it.Icons} icon", metaStyle);

                EditorGUILayout.LabelField("Component — reference only (Unity builds from the layers below)", subStyle);
                if (it.FullTex != null) DrawImage(it.FullTex);

                // Each separated layer in isolation (frame, sub-sprites, icons).
                foreach (var l in it.Layers)
                {
                    EditorGUILayout.LabelField($"↳ {l.Name}  ·  {l.Kind}  ·  {BorderLabel(l.Border)}", subStyle);
                    if (l.Tex != null) DrawImage(l.Tex);
                    else EditorGUILayout.HelpBox("layer crop missing on disk", MessageType.Warning);
                }

                EditorGUILayout.Space(6);
                var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.12f));
                EditorGUILayout.Space(8);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Header: source file, component count, and Refresh / Open-folder actions.</summary>
        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("HTML Preview 🔍", EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (_loaded && GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64)))
                {
                    _loaded = false;
                    _status = "Reloading…";
                    TryLoad();
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_dir) || !Directory.Exists(_dir)))
                    if (GUILayout.Button("Open folder", EditorStyles.miniButton, GUILayout.Width(86)))
                        EditorUtility.RevealInFinder(_dir);
            }
            if (!string.IsNullOrEmpty(_source))
                EditorGUILayout.LabelField(Path.GetFileName(_source) + (_loaded ? $"  ·  {_items.Count} component(s)" : ""), EditorStyles.miniLabel);
            var rule = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rule, new Color(1f, 1f, 1f, 0.12f));
            EditorGUILayout.Space(4);
        }

        /// <summary>"9-slice L T R B" or "no 9-slice" for a layer's border.</summary>
        /// <param name="border">The [L,T,R,B] border, or null.</param>
        /// <returns>A short border label.</returns>
        private static string BorderLabel(int[] border)
        {
            if (border != null && border.Length == 4)
                return $"9-slice L{border[0]} T{border[1]} R{border[2]} B{border[3]}";
            return "no 9-slice";
        }

        /// <summary>Draw a crop scaled to the window width, capped in height, preserving aspect.</summary>
        /// <param name="tex">The crop texture.</param>
        private void DrawImage(Texture2D tex)
        {
            float maxW = Mathf.Max(64f, position.width - 28f);
            float w = Mathf.Min(tex.width, maxW);
            float h = tex.height * (w / tex.width);
            h = Mathf.Min(h, 240f);
            var r = GUILayoutUtility.GetRect(w, h);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
        }

        // ---- preview-index.json DTOs (JsonUtility) ----
        [Serializable] private class PreviewIndex { public string source; public Design design; public PreviewComp[] components; }
        [Serializable] private class Design { public int width; public int height; }
        [Serializable] private class PreviewComp { public string name; public string archetype; public string full; public int texts; public int icons; public PreviewLayer[] layers; }
        [Serializable] private class PreviewLayer { public string name; public string kind; public int[] border; public string file; }
    }
}
