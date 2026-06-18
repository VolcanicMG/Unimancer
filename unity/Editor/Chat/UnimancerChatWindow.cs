using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// In-Editor chat panel (Window → Unimancer → Chat). Talks to the local
    /// <c>claude</c> CLI in headless streaming mode via <see cref="ClaudeCliSession"/>,
    /// so it runs on the user's Claude subscription (no API key) and inherits the full
    /// Unimancer MCP tool surface — the agent loop lives in Claude Code itself.
    ///
    /// Rich features: drag-in / @ object references (expanded into prompt context),
    /// clickable [[unity:...]] handles in replies, and inline tool images.
    /// </summary>
    public class UnimancerChatWindow : EditorWindow
    {
        /// <summary>Where a rendered line came from, for styling.</summary>
        private enum Role { User, Assistant, Tool, System }

        private struct Line
        {
            public Role Role;
            public string Text;
            public Texture2D Tex; // non-null for an inline image line
        }

        // Reuse the Setup window's persisted settings, add chat-only knobs.
        private const string PrefNode = "Unimancer.NodeServerPath";
        private const string PrefWsl = "Unimancer.WrapWsl";
        private const string PrefClaude = "Unimancer.ClaudeCmd";
        private const string PrefModel = "Unimancer.ChatModel";
        private const string PrefTools = "Unimancer.AllowedTools";
        private const string PrefSystemPrompt = "Unimancer.SystemPrompt";
        private const string PrefPermMode = "Unimancer.PermMode";
        private const string PrefSyncSel = "Unimancer.SyncSelection";

        // Default project context appended to the agent's system prompt (editable in Settings).
        private const string DefaultSystemPrompt =
            "You are operating inside the Unity Editor via the Unimancer MCP server.\n" +
            "- This project may use Unity's built-in Version Control (Unity Version Control / Plastic SCM) " +
            "or another VCS instead of git. Do NOT run `git init`, create a git repository, or assume git " +
            "is present. If version control is needed, ask the user which system they use.\n" +
            "- Prefer the Unimancer MCP tools (mcp__unimancer__*) to inspect and modify the project.\n" +
            "- When you reference a Unity object the user can click, write it as " +
            "[[unity:<GlobalObjectId-or-hierarchy-path>]]. Reuse the exact handle from any " +
            "referenced objects the user attached.";

        private readonly List<Line> _lines = new List<Line>();
        private ClaudeCliSession _session;
        private string _sessionSig; // build-params signature; rebuild when Setup/settings change
        private string _input = "";
        private Vector2 _scroll;
        private int _streamIndex = -1;     // index of the assistant line being streamed into
        private bool _gotDeltas;           // did this turn stream any text?
        private bool _showSettings;

        // Cached settings.
        private string _claudeCmd, _model, _allowedTools, _systemPrompt, _permMode;
        private bool _modelCustom; // true = model is a free-form id not in the preset dropdown

        // Current conversation identity (for history persistence / recall).
        private string _convId;     // null until the conversation has content
        private string _convTitle;

        // Pending object references attached to the next message (chips).
        private readonly List<UnityEngine.Object> _refs = new List<UnityEngine.Object>();
        private bool _pickerActive; // an object-picker dialog is open for "@ Reference"

        // Quick-actions state.
        private bool _syncSelection;          // auto-attach the current Selection on every send
        private string _lastError, _lastErrorStack; // most recent Console error, for "Fix last error"

        /// <summary>Open the Unimancer chat window.</summary>
        [MenuItem("Window/Unimancer/Chat")]
        public static void Open()
        {
            var win = GetWindow<UnimancerChatWindow>(false, "Unimancer Chat", true);
            win.minSize = new Vector2(420, 480);
            win.Show();
        }

        private void OnEnable()
        {
            _claudeCmd = EditorPrefs.GetString(PrefClaude, "claude");
            _model = EditorPrefs.GetString(PrefModel, "");
            _allowedTools = EditorPrefs.GetString(PrefTools, "mcp__unimancer");
            _systemPrompt = EditorPrefs.GetString(PrefSystemPrompt, DefaultSystemPrompt);
            _permMode = EditorPrefs.GetString(PrefPermMode, "acceptEdits");
            _syncSelection = EditorPrefs.GetBool(PrefSyncSel, false);
            Application.logMessageReceived += OnConsoleLog;
            _modelCustom = !string.IsNullOrEmpty(_model) && System.Array.IndexOf(ModelValues, _model) < 0;
            EditorApplication.update += Drain;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Drain;
            Application.logMessageReceived -= OnConsoleLog;
            PersistCurrent();
            _session?.Cancel();
        }

        /// <summary>Remember the latest Console error so "Fix last error" can grab it.</summary>
        private void OnConsoleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _lastError = condition;
                _lastErrorStack = stackTrace;
            }
        }

        /// <summary>Lazily build a session bound to the current persisted settings.</summary>
        private ClaudeCliSession EnsureSession()
        {
            // Read connection settings fresh so a change in Window → Unimancer → Setup
            // (e.g. the WSL toggle or the Node path) takes effect without needing New chat.
            bool wsl = EditorPrefs.GetBool(PrefWsl, false);
            string node = EditorPrefs.GetString(PrefNode, "");
            string sig = $"{wsl}|{_claudeCmd}|{node}|{_allowedTools}|{_model}|{_systemPrompt}|{_permMode}";
            if (_session != null && _sessionSig == sig) return _session;
            _session?.Cancel();
            _session = new ClaudeCliSession(wsl, _claudeCmd, node, _allowedTools, _model, _systemPrompt, _permMode);
            _sessionSig = sig;
            return _session;
        }

        /// <summary>Pull queued stream events onto the main thread and update the transcript.</summary>
        private void Drain()
        {
            if (_session == null) return;
            bool changed = false;
            while (_session.Events.TryDequeue(out var ev))
            {
                changed = true;
                switch (ev.Kind)
                {
                    case ChatEventKind.AssistantDelta:
                        _gotDeltas = true;
                        AppendToStream(ev.Text);
                        break;
                    case ChatEventKind.ToolUse:
                        _lines.Add(new Line { Role = Role.Tool, Text = "⚙ " + ev.Text });
                        _streamIndex = -1; // next text starts a fresh assistant line after a tool call
                        break;
                    case ChatEventKind.Result:
                        if (!_gotDeltas && !string.IsNullOrEmpty(ev.Text))
                            AppendToStream(ev.Text);
                        if (ev.IsError)
                            _lines.Add(new Line { Role = Role.System, Text = "⚠ turn failed" });
                        PersistCurrent();
                        break;
                    case ChatEventKind.Error:
                        _lines.Add(new Line { Role = Role.System, Text = "⚠ " + ev.Text });
                        break;
                    case ChatEventKind.Exit:
                        if (ev.IsError) _lines.Add(new Line { Role = Role.System, Text = "⚠ " + ev.Text });
                        break;
                    case ChatEventKind.Image:
                        var tex = DecodeTexture(ev.Text);
                        if (tex != null) { _lines.Add(new Line { Role = Role.Assistant, Tex = tex }); _streamIndex = -1; }
                        break;
                }
            }
            if (changed)
            {
                _scroll.y = float.MaxValue;
                Repaint();
            }
        }

        /// <summary>Append streamed text to the current assistant line, creating it if needed.</summary>
        private void AppendToStream(string text)
        {
            if (_streamIndex < 0 || _streamIndex >= _lines.Count)
            {
                _lines.Add(new Line { Role = Role.Assistant, Text = text });
                _streamIndex = _lines.Count - 1;
            }
            else
            {
                var l = _lines[_streamIndex];
                l.Text += text;
                _lines[_streamIndex] = l;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawQuickBar();
            DrawTranscript();
            DrawInput();
        }

        /// <summary>Toolbar of one-tap prompts + the selection-sync toggle.</summary>
        private void DrawQuickBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Describe scene", EditorStyles.toolbarButton))
                    _input = "Give me a concise overview of the current scene hierarchy and its key GameObjects.";
                if (GUILayout.Button("Explain selection", EditorStyles.toolbarButton))
                {
                    AddSelectionRefs();
                    _input = "Explain the referenced object(s): what they are and how they are used in the project.";
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_lastError)))
                    if (GUILayout.Button("Fix last error", EditorStyles.toolbarButton))
                        _input = "Fix this Unity console error:\n" + _lastError + "\n" + _lastErrorStack;
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                _syncSelection = GUILayout.Toggle(_syncSelection, "Sync selection", EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(PrefSyncSel, _syncSelection);
            }
        }

        /// <summary>Attach the current Editor selection to the pending references.</summary>
        private void AddSelectionRefs()
        {
            foreach (var o in Selection.objects)
                if (o != null && !_refs.Contains(o)) _refs.Add(o);
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Unimancer Chat 🔮", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                bool busy = _session != null && _session.IsBusy;
                var prev = GUI.color;
                GUI.color = busy ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(busy ? "● thinking…" : "○ idle", GUILayout.Width(90));
                GUI.color = prev;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool live = McpBridge.IsListening;
                var prev = GUI.color;
                GUI.color = live ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(live ? "Bridge ● listening" : "Bridge ○ not listening", EditorStyles.miniLabel);
                GUI.color = prev;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("History", EditorStyles.miniButton, GUILayout.Width(64)))
                    ShowHistoryMenu();
                if (GUILayout.Button("New chat", EditorStyles.miniButton, GUILayout.Width(70)))
                    NewConversation();
                _showSettings = GUILayout.Toggle(_showSettings, "Settings", EditorStyles.miniButton, GUILayout.Width(70));
            }

            if (_showSettings) DrawSettings();
            EditorGUILayout.Space(2);
        }

        // Friendly model choices mapped to Claude Code `--model` values.
        // "" = let Claude Code decide; the last entry is a free-form "Custom…" escape hatch.
        private static readonly string[] ModelLabels = { "Default", "Opus", "Sonnet", "Haiku", "Custom…" };
        private static readonly string[] ModelValues = { "", "opus", "sonnet", "haiku", null };
        private static int CustomIdx => ModelLabels.Length - 1;

        // Permission modes: auto-apply edits vs propose-only (read-only planning).
        private static readonly string[] PermLabels = { "Auto-approve", "Plan (propose only)" };
        private static readonly string[] PermValues = { "acceptEdits", "plan" };

        private void DrawSettings()
        {
            EditorGUILayout.HelpBox(
                "Runs your local `claude` CLI on your subscription (no API key). " +
                "Set the Node server path in Window → Unimancer → Setup.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            _claudeCmd = EditorGUILayout.TextField("claude command", _claudeCmd);

            // Model: dropdown of presets, with a Custom row that reveals a text field.
            int idx = _modelCustom ? CustomIdx : System.Array.IndexOf(ModelValues, _model);
            if (idx < 0) idx = CustomIdx;
            int newIdx = EditorGUILayout.Popup("model", idx, ModelLabels);
            if (newIdx != idx)
            {
                _modelCustom = newIdx == CustomIdx;
                if (!_modelCustom) _model = ModelValues[newIdx]; // a preset (keep current text when entering Custom)
                idx = newIdx;
            }
            if (_modelCustom)
                _model = EditorGUILayout.TextField("custom model id", _model);

            _allowedTools = EditorGUILayout.TextField("allowed tools", _allowedTools);

            int pidx = Mathf.Max(0, System.Array.IndexOf(PermValues, _permMode));
            pidx = EditorGUILayout.Popup("permission", pidx, PermLabels);
            _permMode = PermValues[Mathf.Clamp(pidx, 0, PermValues.Length - 1)];

            EditorGUILayout.LabelField("project context (system prompt)");
            _systemPrompt = EditorGUILayout.TextArea(_systemPrompt, GUILayout.MinHeight(64));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefClaude, _claudeCmd ?? "claude");
                EditorPrefs.SetString(PrefModel, _model ?? "");
                EditorPrefs.SetString(PrefTools, _allowedTools ?? "mcp__unimancer");
                EditorPrefs.SetString(PrefSystemPrompt, _systemPrompt ?? "");
                EditorPrefs.SetString(PrefPermMode, _permMode ?? "acceptEdits");
                _session = null; // rebuild with new settings on next send
            }
        }

        private void DrawTranscript()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_lines.Count == 0)
                EditorGUILayout.LabelField("Ask Unimancer to inspect or change your project…", EditorStyles.centeredGreyMiniLabel);

            foreach (var line in _lines)
            {
                if (line.Tex != null) { DrawImageLine(line.Tex); continue; }

                var style = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };
                string prefix;
                switch (line.Role)
                {
                    case Role.User: prefix = "<b>You</b>\n"; break;
                    case Role.Assistant: prefix = "<b>Unimancer</b>\n"; break;
                    case Role.Tool: prefix = ""; style.normal.textColor = new Color(0.55f, 0.7f, 1f); break;
                    default: prefix = ""; style.normal.textColor = new Color(1f, 0.6f, 0.6f); break;
                }

                if (line.Role == Role.User || line.Role == Role.Assistant)
                {
                    DrawRichMessage(line.Role, line.Text, style);
                }
                else
                {
                    EditorGUILayout.LabelField(prefix + EscapeForRichText(line.Text), style);
                }
                EditorGUILayout.Space(4);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Draw an inline image, scaled to fit the window width (capped height).</summary>
        private void DrawImageLine(Texture2D tex)
        {
            float maxW = Mathf.Max(64f, position.width - 30f);
            float w = Mathf.Min(tex.width, maxW);
            float h = tex.height * (w / tex.width);
            h = Mathf.Min(h, 260f);
            var r = GUILayoutUtility.GetRect(w, h);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
            EditorGUILayout.Space(4);
        }

        private static readonly Regex RefRx = new Regex(@"\[\[unity:(.+?)\]\]", RegexOptions.Compiled);

        private static readonly Regex BoldRx = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex CodeRx = new Regex(@"`([^`]+?)`", RegexOptions.Compiled);

        /// <summary>Render a chat message with markdown-lite (headings, bold, code, lists, fences).</summary>
        private void DrawRichMessage(Role role, string text, GUIStyle baseStyle)
        {
            EditorGUILayout.LabelField(role == Role.User ? "<b>You</b>" : "<b>Unimancer</b>", baseStyle);
            var handles = new List<string>();
            foreach (var block in SplitFences(text))
            {
                if (block.isCode) DrawCodeBlock(block.text);
                else DrawMarkdownText(block.text, baseStyle, handles);
            }
            DrawRefLinks(handles);
        }

        /// <summary>Split a message into alternating text / fenced-code blocks (``` delimited).</summary>
        private static List<(bool isCode, string text)> SplitFences(string text)
        {
            var res = new List<(bool, string)>();
            if (string.IsNullOrEmpty(text)) { res.Add((false, text ?? "")); return res; }
            var parts = text.Split(new[] { "```" }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                bool code = i % 2 == 1;
                var seg = parts[i];
                if (code)
                {
                    // Drop an optional language hint on the fence's first line.
                    var nl = seg.IndexOf('\n');
                    if (nl >= 0)
                    {
                        var first = seg.Substring(0, nl).Trim();
                        if (first.Length > 0 && !first.Contains(' ')) seg = seg.Substring(nl + 1);
                    }
                }
                if (code || seg.Length > 0) res.Add((code, seg));
            }
            return res;
        }

        /// <summary>Render a fenced code block: boxed, selectable, with a Copy button.</summary>
        private void DrawCodeBlock(string code)
        {
            code = code.TrimEnd('\n');
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("code", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(48)))
                        EditorGUIUtility.systemCopyBuffer = code;
                    if (GUILayout.Button("Save…", EditorStyles.miniButton, GUILayout.Width(54)))
                        SaveCodeToFile(code);
                }
                var st = new GUIStyle(EditorStyles.label) { wordWrap = false, richText = false };
                int rows = code.Length == 0 ? 1 : code.Split('\n').Length;
                EditorGUILayout.SelectableLabel(code, st, GUILayout.Height(rows * 13 + 6), GUILayout.ExpandWidth(true));
            }
        }

        /// <summary>Write a code block to a user-chosen file (confirming any overwrite).</summary>
        private static void SaveCodeToFile(string code)
        {
            var target = EditorUtility.SaveFilePanel("Save code", Application.dataPath, "NewScript", "cs");
            if (string.IsNullOrEmpty(target)) return;
            if (System.IO.File.Exists(target) &&
                !EditorUtility.DisplayDialog("Overwrite?", "Replace existing file?\n" + target, "Overwrite", "Cancel"))
                return;
            try
            {
                System.IO.File.WriteAllText(target, code);
                if (target.Replace("\\", "/").StartsWith(Application.dataPath.Replace("\\", "/")))
                    AssetDatabase.Refresh();
                Debug.Log("[Unimancer] Saved code to " + target);
            }
            catch (Exception e) { EditorUtility.DisplayDialog("Save failed", e.Message, "OK"); }
        }

        /// <summary>Render a non-code block, line by line, with markdown-lite formatting.</summary>
        private void DrawMarkdownText(string text, GUIStyle baseStyle, List<string> handles)
        {
            foreach (var raw in text.Split('\n'))
            {
                if (raw.Trim().Length == 0) { GUILayout.Space(3); continue; }
                var trimmed = raw.TrimStart();
                string content = raw;
                GUIStyle style = baseStyle;
                if (trimmed.StartsWith("### ")) { content = trimmed.Substring(4); style = Heading(12); }
                else if (trimmed.StartsWith("## ")) { content = trimmed.Substring(3); style = Heading(13); }
                else if (trimmed.StartsWith("# ")) { content = trimmed.Substring(2); style = Heading(15); }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ")) { content = "  • " + trimmed.Substring(2); }
                EditorGUILayout.LabelField(FormatInline(content, handles), style);
            }
        }

        private static GUIStyle Heading(int size) =>
            new GUIStyle(EditorStyles.boldLabel) { richText = true, wordWrap = true, fontSize = size };

        /// <summary>Inline markdown → rich text: escape, **bold**, `code`, and [[unity:]] refs.</summary>
        private static string FormatInline(string text, List<string> handles)
        {
            var s = EscapeForRichText(text);
            s = BoldRx.Replace(s, "<b>$1</b>");
            s = CodeRx.Replace(s, m => "<color=#d7ba7d>" + m.Groups[1].Value + "</color>");
            s = RefRx.Replace(s, m =>
            {
                var h = m.Groups[1].Value;
                handles.Add(h);
                var o = UnityRef.Resolve(h);
                return "<b>" + EscapeForRichText(o != null ? UnityRef.Label(o) : h) + "</b>";
            });
            return s;
        }

        /// <summary>Render a clickable link per referenced object (selects + pings on click).</summary>
        private void DrawRefLinks(List<string> handles)
        {
            if (handles == null || handles.Count == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);
                foreach (var h in handles)
                {
                    var obj = UnityRef.Resolve(h);
                    var label = "↪ " + (obj != null ? UnityRef.Label(obj) : h);
                    if (EditorGUILayout.LinkButton(label)) UnityRef.Ping(obj);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawInput()
        {
            DrawReferenceBar();
            using (new EditorGUILayout.HorizontalScope())
            {
                // Ctrl+Enter sends.
                var e = Event.current;
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && (e.control || e.command))
                {
                    SendCurrent();
                    e.Use();
                }

                _input = EditorGUILayout.TextArea(_input, GUILayout.Height(48));

                using (new EditorGUI.DisabledScope(_session != null && _session.IsBusy))
                {
                    if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(48)))
                        SendCurrent();
                }
                if (_session != null && _session.IsBusy && GUILayout.Button("Stop", GUILayout.Width(46), GUILayout.Height(48)))
                    _session.Cancel();
            }
            EditorGUILayout.LabelField("Ctrl+Enter to send", EditorStyles.miniLabel);
        }

        private void SendCurrent()
        {
            var msg = (_input ?? "").Trim();
            if (msg.Length == 0 && _refs.Count == 0) return;
            var s = EnsureSession();
            if (s.IsBusy) return;

            if (_syncSelection) AddSelectionRefs();

            // Expand attached references into prompt context; keep the transcript tidy.
            var sent = BuildPrompt(msg);
            var refNames = _refs.Where(o => o != null).Select(o => o.name).ToArray();
            var display = msg + (refNames.Length > 0 ? "  ⟨refs: " + string.Join(", ", refNames) + "⟩" : "");

            if (string.IsNullOrEmpty(_convId))
            {
                _convId = Guid.NewGuid().ToString("N");
                var t = string.IsNullOrEmpty(msg) ? (refNames.Length > 0 ? refNames[0] : "(chat)") : msg;
                _convTitle = t.Length > 48 ? t.Substring(0, 48) + "…" : t;
            }
            _lines.Add(new Line { Role = Role.User, Text = display });
            _refs.Clear();
            _input = "";
            _streamIndex = -1;
            _gotDeltas = false;
            PersistCurrent();
            GUI.FocusControl(null);
            s.Send(sent);
        }

        /// <summary>Build the prompt sent to claude: user text + a context block for attached refs.</summary>
        private string BuildPrompt(string msg)
        {
            if (_refs.Count == 0) return msg;
            var sb = new StringBuilder(msg);
            sb.Append("\n\n## Referenced Unity objects\n");
            foreach (var o in _refs)
                if (o != null) sb.Append(UnityRef.Describe(o)).Append('\n');
            return sb.ToString();
        }

        /// <summary>Reference bar: drag target, @ picker, Use selection, and removable chips.</summary>
        private void DrawReferenceBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("@ Reference", EditorStyles.miniButton, GUILayout.Width(92)))
                {
                    _pickerActive = true;
                    EditorGUIUtility.ShowObjectPicker<UnityEngine.Object>(null, true, "", GUIUtility.GetControlID(FocusType.Passive));
                }
                if (GUILayout.Button("Use selection", EditorStyles.miniButton, GUILayout.Width(100)))
                    foreach (var o in Selection.objects)
                        if (o != null && !_refs.Contains(o)) _refs.Add(o);
                if (_refs.Count > 0 && GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                    _refs.Clear();
                GUILayout.FlexibleSpace();
            }

            // Capture the object-picker result.
            if (_pickerActive && Event.current.commandName == "ObjectSelectorClosed")
            {
                var picked = EditorGUIUtility.GetObjectPickerObject();
                if (picked != null && !_refs.Contains(picked)) _refs.Add(picked);
                _pickerActive = false;
            }

            // Drop area.
            var drop = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            GUI.Box(drop, _refs.Count == 0 ? "Drag GameObjects / assets here to reference them" : "");
            HandleDrop(drop);

            // Chips (click to remove).
            if (_refs.Count > 0)
            {
                int remove = -1;
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < _refs.Count; i++)
                    {
                        var o = _refs[i];
                        var label = (o != null ? o.name : "(missing)") + "  ✕";
                        if (GUILayout.Button(label, EditorStyles.miniButton)) remove = i;
                    }
                    GUILayout.FlexibleSpace();
                }
                if (remove >= 0) _refs.RemoveAt(remove);
            }
        }

        /// <summary>Accept dropped objects into the reference list.</summary>
        private void HandleDrop(Rect area)
        {
            var e = Event.current;
            if (!area.Contains(e.mousePosition)) return;
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var o in DragAndDrop.objectReferences)
                    if (o != null && !_refs.Contains(o)) _refs.Add(o);
                e.Use();
            }
        }

        /// <summary>Decode a base64 image into a throwaway texture for inline display.</summary>
        private static Texture2D DecodeTexture(string b64)
        {
            try
            {
                var bytes = Convert.FromBase64String(b64);
                var t = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (t.LoadImage(bytes)) return t;
            }
            catch { /* not an image */ }
            return null;
        }

        /// <summary>Save the current conversation (if it has content) to the history store.</summary>
        private void PersistCurrent()
        {
            if (string.IsNullOrEmpty(_convId) || _lines.Count == 0) return;
            var c = new ChatConversation
            {
                id = _convId,
                title = string.IsNullOrEmpty(_convTitle) ? "(untitled)" : _convTitle,
                sessionId = _session != null ? _session.SessionId : null,
                lines = new List<ChatLineDto>()
            };
            foreach (var l in _lines)
            {
                if (l.Tex != null) continue; // images are ephemeral, not persisted
                c.lines.Add(new ChatLineDto { role = (int)l.Role, text = l.Text });
            }
            ChatHistoryStore.Save(c);
        }

        /// <summary>Persist the current chat, then start a fresh one.</summary>
        private void NewConversation()
        {
            PersistCurrent();
            _session?.Cancel();
            _session = null;
            _lines.Clear();
            _streamIndex = -1;
            _convId = null;
            _convTitle = null;
        }

        /// <summary>Drop-down of saved conversations; selecting one reloads and resumes it.</summary>
        private void ShowHistoryMenu()
        {
            PersistCurrent(); // don't lose the current chat while browsing
            var menu = new GenericMenu();
            var all = ChatHistoryStore.LoadAll();
            if (all.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("(no saved chats)"));
            }
            else
            {
                foreach (var c in all)
                {
                    var when = new DateTime(c.updatedTicks).ToLocalTime().ToString("MM-dd HH:mm");
                    // '/' would create submenus in GenericMenu — flatten it.
                    var label = (when + "  " + (c.title ?? "")).Replace("/", " ");
                    var captured = c;
                    menu.AddItem(new GUIContent(label), captured.id == _convId, () => LoadConversation(captured.id));
                }
                menu.AddSeparator("");
                foreach (var c in all)
                {
                    var when = new DateTime(c.updatedTicks).ToLocalTime().ToString("MM-dd HH:mm");
                    var label = (when + "  " + (c.title ?? "")).Replace("/", " ");
                    var captured = c;
                    menu.AddItem(new GUIContent("Delete/" + label), false, () => DeleteConversation(captured.id));
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete all chats…"), false, DeleteAllConversations);
            }
            menu.ShowAsContext();
        }

        /// <summary>Load a saved conversation into the window and seed its session id for --resume.</summary>
        private void LoadConversation(string id)
        {
            var c = ChatHistoryStore.Load(id);
            if (c == null) return;
            _session?.Cancel();
            _session = null; // rebuilt on next send with current settings
            _lines.Clear();
            foreach (var dto in c.lines)
                _lines.Add(new Line { Role = (Role)dto.role, Text = dto.text });
            _convId = c.id;
            _convTitle = c.title;
            _streamIndex = -1;
            if (!string.IsNullOrEmpty(c.sessionId))
                EnsureSession().Resume(c.sessionId); // continue the saved Claude Code session
            _scroll.y = float.MaxValue;
            Repaint();
        }

        /// <summary>Delete one saved conversation (after confirm); resets the view if it was open.</summary>
        private void DeleteConversation(string id)
        {
            var c = ChatHistoryStore.Load(id);
            var title = c != null ? c.title : id;
            if (!EditorUtility.DisplayDialog("Delete chat", $"Delete \"{title}\"? This cannot be undone.", "Delete", "Cancel"))
                return;
            ChatHistoryStore.Delete(id);
            if (id == _convId) { _convId = null; _convTitle = null; } // stop it being re-saved
        }

        /// <summary>Delete every saved conversation (after confirm).</summary>
        private void DeleteAllConversations()
        {
            if (!EditorUtility.DisplayDialog("Delete all chats", "Delete ALL saved chats? This cannot be undone.", "Delete all", "Cancel"))
                return;
            foreach (var c in ChatHistoryStore.LoadAll()) ChatHistoryStore.Delete(c.id);
            _convId = null;
            _convTitle = null;
        }

        /// <summary>Escape angle brackets in model text so it doesn't break our richText labels.</summary>
        private static string EscapeForRichText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
