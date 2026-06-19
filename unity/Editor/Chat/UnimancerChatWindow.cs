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
        private enum Role { User, Assistant, Tool, System, Diff }

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
        private const string PrefLastConv = "Unimancer.LastConvId"; // reopen the last chat after a recompile/restart

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
        private double _turnStart;      // EditorApplication.timeSinceStartup when the current turn began
        private double _lastTimerTick;  // throttles the live "Working…" repaint
        private Vector2 _inputScroll; // vertical scroll inside the fixed-height input box
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
        private bool _dragHover;    // a GameObject/asset drag is currently hovering the window

        // Follow-ups typed while a turn is streaming; auto-sent when it finishes.
        private struct Pending { public string Display; public string Prompt; public string Title; }
        private readonly List<Pending> _queued = new List<Pending>();

        // A pending "ask the user" prompt parsed from the agent's reply (rendered as buttons).
        private string _askQuestion;
        private readonly List<string> _askOptions = new List<string>();

        // Screenshots/images (file paths) attached to the next message.
        private readonly List<string> _shots = new List<string>();
        private volatile string _pendingPaste;   // set off-thread by a clipboard-image grab, consumed in Drain
        private bool _pasteChecking;              // a clipboard grab is in flight

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

            // Reopen the chat we were in before a domain reload (recompile / Play mode) so
            // the window returns to where it was. Private fields don't survive a reload,
            // but the transcript + Claude session id are on disk, so we restore both and
            // seed --resume for the next message.
            if (_lines.Count == 0)
            {
                var last = EditorPrefs.GetString(PrefLastConv, "");
                if (!string.IsNullOrEmpty(last))
                {
                    LoadConversation(last);
                    if (_lines.Count > 0 && _lines[_lines.Count - 1].Role == Role.User)
                        _lines.Add(new Line { Role = Role.System, Text = "↻ Restored after recompile — the previous reply was interrupted; send a message to continue." });
                }
            }
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

        /// <summary>
        /// Static environment context appended to the system prompt so the agent knows it
        /// is the assistant inside the Unimancer Chat panel embedded in the Unity Editor
        /// (not a terminal), talking to a Unity developer working in the open project.
        /// </summary>
        /// <returns>A short markdown context block.</returns>
        private static string BuildEditorContext()
        {
            return
                "## Where you are\n" +
                "You are the assistant in the **Unimancer Chat** panel — a window embedded inside the " +
                "Unity Editor (Window -> Unimancer -> Chat). The user is a Unity developer working live in " +
                "the open project; your replies render in that panel and your MCP tools act on the live " +
                "Editor. Keep answers practical for someone in the Editor right now.\n" +
                $"- Unity version: {Application.unityVersion}\n" +
                $"- Project: {Application.productName}\n" +
                $"- Active build target: {EditorUserBuildSettings.activeBuildTarget}\n" +
                "- A domain reload (script recompile / entering Play mode) restarts this chat process; if a " +
                "turn is cut off, the conversation auto-resumes on the next message.\n\n" +
                "## Asking the user to choose\n" +
                "When you want the user to pick between options, END your reply with a fenced block:\n" +
                "```unimancer:ask\n<your question>\n- First option\n- Second option\n```\n" +
                "The panel turns the options into clickable buttons. Use it only for genuine choices, keep " +
                "each option short (a few words), and the user can always type a free-form reply instead.\n\n" +
                "## Screenshots\n" +
                "The user can attach screenshots. When a message lists attached image paths, use the Read " +
                "tool on each path to view the image.";
        }

        /// <summary>Lazily build a session bound to the current persisted settings.</summary>
        private ClaudeCliSession EnsureSession()
        {
            // Read connection settings fresh so a change in Window → Unimancer → Setup
            // (e.g. the WSL toggle or the Node path) takes effect without needing New chat.
            bool wsl = EditorPrefs.GetBool(PrefWsl, false);
            string node = EditorPrefs.GetString(PrefNode, "");
            string effectiveSystem = _systemPrompt + "\n\n" + BuildEditorContext();
            string sig = $"{wsl}|{_claudeCmd}|{node}|{_allowedTools}|{_model}|{effectiveSystem}|{_permMode}";
            if (_session != null && _sessionSig == sig) return _session;
            _session?.Cancel();
            _session = new ClaudeCliSession(wsl, _claudeCmd, node, _allowedTools, _model, effectiveSystem, _permMode);
            _sessionSig = sig;
            return _session;
        }

        /// <summary>Pull queued stream events onto the main thread and update the transcript.</summary>
        private void Drain()
        {
            // A background clipboard grab finished → attach the saved image (size-guarded).
            if (_pendingPaste != null)
            {
                AddShot(_pendingPaste);
                _pendingPaste = null;
            }
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
                    case ChatEventKind.Diff:
                        _lines.Add(new Line { Role = Role.Diff, Text = ev.Text });
                        _streamIndex = -1;
                        break;
                    case ChatEventKind.Result:
                        if (!_gotDeltas && !string.IsNullOrEmpty(ev.Text))
                            AppendToStream(ev.Text);
                        if (ev.IsError)
                            _lines.Add(new Line { Role = Role.System, Text = "⚠ turn failed" });
                        ParseAsk();
                        PersistCurrent();
                        break;
                    case ChatEventKind.Error:
                        _lines.Add(new Line { Role = Role.System, Text = "⚠ " + ev.Text });
                        break;
                    case ChatEventKind.Exit:
                        if (ev.IsError) _lines.Add(new Line { Role = Role.System, Text = "⚠ " + ev.Text });
                        _lines.Add(new Line { Role = Role.Tool, Text = "✦ Worked for " + FormatDuration(EditorApplication.timeSinceStartup - _turnStart) });
                        break;
                    case ChatEventKind.Image:
                        var tex = DecodeTexture(ev.Text);
                        if (tex != null) { _lines.Add(new Line { Role = Role.Assistant, Tex = tex }); _streamIndex = -1; }
                        break;
                }
            }
            // The turn just ended and a follow-up is waiting → send the next one.
            if (_session != null && !_session.IsBusy && _queued.Count > 0)
            {
                var next = _queued[0];
                _queued.RemoveAt(0);
                DispatchTurn(next);
                changed = true;
            }
            // Keep the inline "Working…" timer updating even when no events arrive.
            if (_session != null && _session.IsBusy && EditorApplication.timeSinceStartup - _lastTimerTick >= 0.5)
            {
                _lastTimerTick = EditorApplication.timeSinceStartup;
                Repaint();
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
            // Whole-window drag target: grab drop events before child controls
            // (e.g. the input TextArea) can swallow them, then draw the overlay last.
            HandleWindowDrop();
            DrawHeader();
            DrawQuickBar();
            DrawTranscript();
            DrawInput();
            if (_dragHover) DrawDropOverlay();
        }

        /// <summary>Toolbar of one-tap prompts + the selection-sync toggle.</summary>
        private void DrawQuickBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Describe scene", EditorStyles.toolbarButton))
                    SetInput("Give me a concise overview of the current scene hierarchy and its key GameObjects.");
                if (GUILayout.Button("Explain selection", EditorStyles.toolbarButton))
                {
                    AddSelectionRefs();
                    SetInput("Explain the referenced object(s): what they are and how they are used in the project.");
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_lastError)))
                    if (GUILayout.Button("Fix last error", EditorStyles.toolbarButton))
                        SetInput("Fix this Unity console error:\n" + _lastError + "\n" + _lastErrorStack);
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                _syncSelection = GUILayout.Toggle(_syncSelection, "Sync selection", EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(PrefSyncSel, _syncSelection);
            }
        }

        /// <summary>
        /// Set the input text from code (the quick-prompt buttons) and drop keyboard
        /// focus, so the TextArea reloads from <see cref="_input"/> on the next repaint
        /// instead of showing its cached edit buffer — without this, a button press only
        /// appears after the field loses focus, which felt like "it doesn't populate".
        /// </summary>
        /// <param name="text">Text to place in the input box.</param>
        private void SetInput(string text)
        {
            _input = text;
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            Repaint();
        }

        /// <summary>Attach the current Editor selection to the pending references.</summary>
        private void AddSelectionRefs()
        {
            foreach (var o in Selection.objects)
                if (o != null && !_refs.Contains(o)) _refs.Add(o);
        }

        // Cached package version, read from the UPM package.json so the number
        // shown in the UI never drifts from the actual release.
        private static string _version;

        /// <summary>
        /// Unimancer package version, sourced from <c>package.json</c> via the Package
        /// Manager (so it reflects the real release, not a hand-maintained constant).
        /// Resolved once and cached; falls back to the known package path, then "dev".
        /// </summary>
        private static string Version
        {
            get
            {
                if (!string.IsNullOrEmpty(_version)) return _version;
                try
                {
                    var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                        typeof(UnimancerChatWindow).Assembly)
                        ?? UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                            "Packages/com.unimancer.mcp/package.json");
                    if (info != null && !string.IsNullOrEmpty(info.version))
                        return _version = info.version;
                }
                catch { /* fall through */ }
                return _version = "dev";
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Unimancer Chat 🔮", EditorStyles.boldLabel, GUILayout.Width(140));
                var vStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
                GUILayout.Label("v" + Version, vStyle);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool live = McpBridge.IsListening;
                var prev = GUI.color;
                GUI.color = live ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(live ? "Bridge ● listening" : "Bridge ○ not listening", EditorStyles.miniLabel);
                GUI.color = prev;
                // One-click recovery when the bridge is wedged / its port was taken.
                if (!live && GUILayout.Button("Restart", EditorStyles.miniButton, GUILayout.Width(60)))
                    McpBridge.Restart();
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
            EditorGUILayout.LabelField("Unimancer MCP  v" + Version, EditorStyles.miniLabel);
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

            // Keep the chat alive through Play mode by skipping Unity's domain reload —
            // a domain reload wipes the C# heap and kills the chat process mid-turn.
            EditorGUILayout.Space(2);
            bool survive = EditorSettings.enterPlayModeOptionsEnabled
                           && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            bool newSurvive = EditorGUILayout.ToggleLeft(
                new GUIContent("Keep chat alive in Play mode",
                    "Disables Unity's domain reload when entering Play, so the chat (and its process) keep running instead of being interrupted. Side effect: static fields and event subscriptions are NOT reset between Play sessions — your game code must handle that."),
                survive);
            if (newSurvive != survive)
            {
                if (newSurvive)
                {
                    EditorSettings.enterPlayModeOptionsEnabled = true;
                    EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
                }
                else
                {
                    EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableDomainReload;
                    if (EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.None)
                        EditorSettings.enterPlayModeOptionsEnabled = false;
                }
            }
            if (newSurvive)
                EditorGUILayout.HelpBox(
                    "Domain reload on Play is OFF, so the chat survives Play. But statics/events now persist between Play "
                    + "sessions — reset them yourself (e.g. [RuntimeInitializeOnLoadMethod]) if your game relies on fresh state. "
                    + "A script recompile still reloads (the chat auto-resumes then).", MessageType.Info);
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
                else if (line.Role == Role.Diff)
                {
                    DrawDiff(line.Text);
                }
                else
                {
                    EditorGUILayout.LabelField(prefix + EscapeForRichText(line.Text), style);
                }
                EditorGUILayout.Space(4);
            }

            // Inline live status (replaces the old header "thinking" label).
            if (_session != null && _session.IsBusy)
            {
                var work = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
                EditorGUILayout.LabelField("✦ Working… " + FormatDuration(EditorApplication.timeSinceStartup - _turnStart), work);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Human-readable duration: "8s" or "1m 12s".</summary>
        private static string FormatDuration(double seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
            return s < 60 ? s + "s" : (s / 60) + "m " + (s % 60) + "s";
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

        /// <summary>Render a prefixed diff (from ClaudeCliSession.BuildDiff) as a colored block.</summary>
        /// <param name="raw">Lines prefixed "- " removed / "+ " added / "§ " header.</param>
        private void DrawDiff(string raw)
        {
            var sb = new StringBuilder();
            foreach (var ln in (raw ?? "").Split('\n'))
            {
                string color = null;
                if (ln.StartsWith("+")) color = "#6ac46a";        // added → green
                else if (ln.StartsWith("-")) color = "#e06c6c";   // removed → red
                else if (ln.StartsWith("§")) color = "#9aa0a6";   // header → grey
                var e = EscapeForRichText(ln);
                if (color != null) sb.Append("<color=").Append(color).Append('>').Append(e).Append("</color>\n");
                else sb.Append(e).Append('\n');
            }
            var style = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true, fontSize = 11 };
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                EditorGUILayout.LabelField(sb.ToString().TrimEnd('\n'), style);
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

        // Matches a ```unimancer:ask ... ``` block the agent emits to offer the user a choice.
        private static readonly Regex AskRx = new Regex(@"```unimancer:ask\s*\n(.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Pull a ```unimancer:ask``` block out of the latest assistant reply into clickable
        /// options and strip the raw block from the shown text. Drives <see cref="DrawAsk"/>.
        /// </summary>
        private void ParseAsk()
        {
            _askQuestion = null;
            _askOptions.Clear();

            int idx = -1;
            for (int i = _lines.Count - 1; i >= 0; i--)
                if (_lines[i].Role == Role.Assistant && _lines[i].Tex == null) { idx = i; break; }
            if (idx < 0) return;

            var line = _lines[idx];
            var m = AskRx.Match(line.Text ?? "");
            if (!m.Success) return;

            var q = new StringBuilder();
            foreach (var raw in m.Groups[1].Value.Split('\n'))
            {
                var t = raw.Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("- ")) _askOptions.Add(t.Substring(2).Trim());
                else if (t.StartsWith("question:")) q.Append(t.Substring(9).Trim()).Append(' ');
                else q.Append(t).Append(' ');
            }
            _askQuestion = q.ToString().Trim();

            // Strip the raw block from the visible reply; keep the question text.
            line.Text = AskRx.Replace(line.Text, "").TrimEnd();
            _lines[idx] = line;
        }

        /// <summary>Render the agent's offered choices as buttons; clicking one sends it as the reply.</summary>
        private void DrawAsk()
        {
            if (_askOptions.Count == 0) return;
            string chosen = null;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrEmpty(_askQuestion))
                    EditorGUILayout.LabelField(_askQuestion, EditorStyles.wordWrappedLabel);
                foreach (var opt in _askOptions)
                    if (GUILayout.Button(opt, GUILayout.MinHeight(22))) chosen = opt;
                EditorGUILayout.LabelField("…or type your own reply below.", EditorStyles.miniLabel);
            }
            if (chosen != null)
            {
                var pending = new Pending { Display = chosen, Prompt = BuildPrompt(chosen), Title = chosen };
                _askQuestion = null;
                _askOptions.Clear();
                GUI.FocusControl(null);
                if (EnsureSession().IsBusy) _queued.Add(pending); else DispatchTurn(pending);
            }
        }

        /// <summary>Show queued follow-ups; they auto-send when the current turn ends.</summary>
        private void DrawQueued()
        {
            if (_queued.Count == 0) return;
            int remove = -1;
            for (int i = 0; i < _queued.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("⏳ queued", EditorStyles.miniLabel, GUILayout.Width(58));
                    var d = _queued[i].Display ?? "";
                    GUILayout.Label(d.Length > 80 ? d.Substring(0, 80) + "…" : d, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) remove = i;
                }
            }
            if (remove >= 0) _queued.RemoveAt(remove);
        }

        private void DrawInput()
        {
            DrawReferenceBar();
            DrawAsk();
            DrawQueued();
            using (new EditorGUILayout.HorizontalScope())
            {
                // Ctrl+Enter sends.
                var e = Event.current;
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && (e.control || e.command))
                {
                    SendCurrent();
                    e.Use();
                }
                // Ctrl/Cmd+V: also try to attach a clipboard image (text paste is left intact).
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.V && (e.control || e.command))
                    TryPasteImage();

                bool busy = _session != null && _session.IsBusy;

                // FIXED-height input box: it never changes size, so it can't push the
                // Send button. The inner TextArea has a fixed WIDTH so text wraps (no
                // horizontal scroll); its height tracks the content so a VERTICAL
                // scrollbar appears once the message is taller than the 56px box.
                var inputStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                float wrapWidth = Mathf.Max(60f, EditorGUIUtility.currentViewWidth - 100f);
                float contentH = inputStyle.CalcHeight(new GUIContent(_input), wrapWidth);
                _inputScroll = EditorGUILayout.BeginScrollView(
                    _inputScroll, false, false,
                    GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView,
                    GUILayout.Height(56f), GUILayout.ExpandWidth(true));
                GUI.SetNextControlName("UnimancerInput");
                // Horizontal scrolling is disabled above, so ExpandWidth makes the field
                // fill the visible viewport and the wordWrap style wraps text at that edge;
                // the content-based height lets it scroll vertically inside the 56px box.
                _input = EditorGUILayout.TextArea(_input, inputStyle, GUILayout.ExpandWidth(true), GUILayout.Height(Mathf.Max(contentH, 50f)));

                // Placeholder over the empty, unfocused field (IMGUI has no native one).
                if (string.IsNullOrEmpty(_input)
                    && GUI.GetNameOfFocusedControl() != "UnimancerInput"
                    && Event.current.type == EventType.Repaint)
                {
                    var ph = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        padding = new RectOffset(4, 4, 3, 3),
                        normal = { textColor = new Color(1f, 1f, 1f, 0.35f) },
                    };
                    GUI.Label(GUILayoutUtility.GetLastRect(),
                        "Type a message\u2026  or drag GameObjects / assets here to reference them", ph);
                }
                EditorGUILayout.EndScrollView();

                // Send / Stop in a fixed-width column the input can never displace.
                // Send stays enabled while busy — it queues the message as a follow-up.
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(62)))
                {
                    if (GUILayout.Button(busy ? "Queue" : "Send", GUILayout.Height(busy ? 28f : 56f)))
                        SendCurrent();
                    if (busy && GUILayout.Button("Stop", GUILayout.Height(24f)))
                        _session.Cancel();
                }
            }
            EditorGUILayout.LabelField("Ctrl+Enter to send", EditorStyles.miniLabel);
        }

        private void SendCurrent()
        {
            var msg = (_input ?? "").Trim();
            if (msg.Length == 0 && _refs.Count == 0 && _shots.Count == 0) return;

            if (_syncSelection) AddSelectionRefs();

            // Expand attached references into prompt context; keep the transcript tidy.
            var sent = BuildPrompt(msg);
            var refNames = _refs.Where(o => o != null).Select(o => o.name).ToArray();
            var display = msg
                + (refNames.Length > 0 ? "  ⟨refs: " + string.Join(", ", refNames) + "⟩" : "")
                + (_shots.Count > 0 ? "  📷×" + _shots.Count : "");
            var title = string.IsNullOrEmpty(msg) ? (refNames.Length > 0 ? refNames[0] : "(chat)") : msg;
            var pending = new Pending { Display = display, Prompt = sent, Title = title };

            _refs.Clear();
            _shots.Clear();
            _input = "";
            GUI.FocusControl(null);

            // If a turn is already streaming, queue this as a follow-up (auto-sends when
            // the current turn ends) instead of dropping it — the user can keep typing.
            var s = EnsureSession();
            if (s.IsBusy)
            {
                _queued.Add(pending);
                Repaint();
                return;
            }
            DispatchTurn(pending);
        }

        /// <summary>Begin a turn: add the user line, persist, and stream the reply.</summary>
        /// <param name="p">The pending message (display text, sent prompt, title hint).</param>
        private void DispatchTurn(Pending p)
        {
            _askQuestion = null;
            _askOptions.Clear();
            _turnStart = EditorApplication.timeSinceStartup;
            var s = EnsureSession();
            if (string.IsNullOrEmpty(_convId))
            {
                _convId = Guid.NewGuid().ToString("N");
                _convTitle = p.Title != null && p.Title.Length > 48 ? p.Title.Substring(0, 48) + "…" : p.Title;
            }
            _lines.Add(new Line { Role = Role.User, Text = p.Display });
            _streamIndex = -1;
            _gotDeltas = false;
            _scroll.y = float.MaxValue; // jump to the bottom on send even if scrolled up
            PersistCurrent();
            s.Send(p.Prompt);
            Repaint();
        }

        /// <summary>Build the prompt sent to claude: user text + a context block for attached refs.</summary>
        private string BuildPrompt(string msg)
        {
            var sb = new StringBuilder(msg);

            // Fresh per-message context so the agent knows what the user is looking at now.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            sb.Append("\n\n## Current editor context\n");
            sb.Append("- Active scene: ").Append(string.IsNullOrEmpty(scene.name) ? "(untitled)" : scene.name).Append('\n');
            sb.Append("- Mode: ").Append(EditorApplication.isPlaying ? "Play mode" : "Edit mode").Append('\n');
            sb.Append("- Current selection: ").Append(Selection.objects.Length).Append(" object(s)\n");

            if (_refs.Count > 0)
            {
                sb.Append("\n## Referenced Unity objects\n");
                foreach (var o in _refs)
                    if (o != null) sb.Append(UnityRef.Describe(o)).Append('\n');
            }

            if (_shots.Count > 0)
            {
                bool wsl = EditorPrefs.GetBool(PrefWsl, false);
                sb.Append("\n## Attached screenshots\n");
                sb.Append("The user attached image(s); use the Read tool on each path to view them:\n");
                foreach (var p in _shots)
                    sb.Append("- ").Append(ToShellPath(wsl, p)).Append('\n');
            }
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
                if (GUILayout.Button(new GUIContent("📷 View", "Screenshot the Game view (or Scene view) and attach it"), EditorStyles.miniButton, GUILayout.Width(66)))
                    CaptureViewToAttachment();
                if (GUILayout.Button(new GUIContent("🖼 File", "Attach an image file from disk"), EditorStyles.miniButton, GUILayout.Width(58)))
                    AttachImageFile();
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

            // Dropping is handled window-wide by HandleWindowDrop(); no dedicated
            // strip here anymore. Chips below show what's currently attached.

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

            // Attached screenshots (click to remove).
            if (_shots.Count > 0)
            {
                int rm = -1;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("📷", GUILayout.Width(18));
                    for (int i = 0; i < _shots.Count; i++)
                        if (GUILayout.Button(System.IO.Path.GetFileName(_shots[i]) + "  ✕", EditorStyles.miniButton)) rm = i;
                    GUILayout.FlexibleSpace();
                }
                if (rm >= 0) _shots.RemoveAt(rm);
            }
        }

        /// <summary>
        /// Make the entire window a drop target for GameObjects / assets. Runs
        /// before the child controls in OnGUI so the input TextArea can't swallow
        /// the drag; sets <see cref="_dragHover"/> so the overlay can be drawn last.
        /// </summary>
        private void HandleWindowDrop()
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.DragUpdated:
                    // Accept Unity objects, or image files dragged from the OS.
                    if (DragAndDrop.objectReferences.Length == 0 && !AnyImagePath(DragAndDrop.paths)) break;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (!_dragHover) { _dragHover = true; Repaint(); }
                    e.Use();
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    foreach (var o in DragAndDrop.objectReferences)
                        if (o != null && !_refs.Contains(o)) _refs.Add(o);
                    // OS file drags carry no objectReferences — treat image paths as screenshots.
                    if (DragAndDrop.objectReferences.Length == 0)
                        foreach (var p in DragAndDrop.paths)
                            if (IsImagePath(p)) AddShot(p);
                    _dragHover = false;
                    e.Use();
                    Repaint();
                    break;
                case EventType.DragExited:
                    if (_dragHover) { _dragHover = false; Repaint(); }
                    break;
            }
        }

        /// <summary>Render the Game view (or Scene view) to a temp PNG and attach it to the next message.</summary>
        private void CaptureViewToAttachment()
        {
            try
            {
                var cam = Camera.main ?? Camera.allCameras.FirstOrDefault(c => c.enabled);
                if (cam == null)
                {
                    var sv = SceneView.lastActiveSceneView;
                    cam = sv != null ? sv.camera : null;
                }
                if (cam == null) { ShowNotification(new GUIContent("No camera or Scene view to capture")); return; }

                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unimancer-shot-" + Guid.NewGuid().ToString("N") + ".png");
                CaptureUtil.RenderCameraToPng(cam, 1280, 720, path);
                AddShot(path);
            }
            catch (Exception e) { Debug.LogWarning("[Unimancer] Capture failed: " + e.Message); }
        }

        /// <summary>Attach an image path, skipping blank/empty grabs (e.g. an empty clipboard image).</summary>
        /// <param name="path">Absolute image file path.</param>
        private void AddShot(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
                if (new System.IO.FileInfo(path).Length < 1024) return; // a real screenshot is far bigger; skip blanks
                if (!_shots.Contains(path)) { _shots.Add(path); Repaint(); }
            }
            catch { /* ignore unreadable path */ }
        }

        /// <summary>Open a file picker and attach an image to send with the next message.</summary>
        private void AttachImageFile()
        {
            var path = EditorUtility.OpenFilePanel("Attach image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path)) AddShot(path);
        }

        /// <summary>
        /// Attach an image sitting on the OS clipboard (e.g. a Win+Shift+S snip). Unity's
        /// IMGUI clipboard is text-only, so we grab the bitmap via a short PowerShell call
        /// (STA, off the UI thread) that saves it to a temp PNG; <see cref="Drain"/> picks
        /// up the result. No-op if the clipboard holds no image.
        /// </summary>
        private void TryPasteImage()
        {
            if (_pasteChecking) return;
            try
            {
                var outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unimancer-paste-" + Guid.NewGuid().ToString("N") + ".png");
                var ps = "Add-Type -AssemblyName System.Windows.Forms,System.Drawing; " +
                         "$i=[System.Windows.Forms.Clipboard]::GetImage(); " +
                         "if($i -and $i.Width -gt 8 -and $i.Height -gt 8){$i.Save('" + outPath + "',[System.Drawing.Imaging.ImageFormat]::Png)}";
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -STA -Command \"" + ps + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var proc = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
                _pasteChecking = true;
                proc.Exited += (_, __) =>
                {
                    _pasteChecking = false;
                    // Marshal back to the main thread via the field Drain() polls.
                    if (System.IO.File.Exists(outPath)) _pendingPaste = outPath;
                };
                proc.Start();
            }
            catch (Exception e)
            {
                _pasteChecking = false;
                Debug.LogWarning("[Unimancer] Clipboard image paste failed (Windows only): " + e.Message);
            }
        }

        /// <summary>True if any path looks like an image we can attach.</summary>
        private static bool AnyImagePath(string[] paths)
        {
            if (paths == null) return false;
            foreach (var p in paths) if (IsImagePath(p)) return true;
            return false;
        }

        /// <summary>True if the path has a supported image extension.</summary>
        private static bool IsImagePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            var e = System.IO.Path.GetExtension(p).ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg";
        }

        /// <summary>Translate a Windows path to the form the claude shell sees (WSL mount when wrapping).</summary>
        private static string ToShellPath(bool wsl, string winPath)
        {
            if (!wsl || string.IsNullOrEmpty(winPath) || winPath.Length < 2 || winPath[1] != ':') return winPath;
            var drive = char.ToLowerInvariant(winPath[0]);
            return "/mnt/" + drive + winPath.Substring(2).Replace('\\', '/');
        }

        /// <summary>Tint + label drawn over the whole window while a drag hovers.</summary>
        private void DrawDropOverlay()
        {
            if (Event.current.type != EventType.Repaint) return;
            var win = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(win, new Color(0.20f, 0.55f, 0.95f, 0.12f));
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Color.white },
            };
            GUI.Label(win, "\u2B07  Drop GameObjects, assets, or images here", style);
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
            EditorPrefs.SetString(PrefLastConv, _convId);
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
            EditorPrefs.DeleteKey(PrefLastConv);
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
            EditorPrefs.SetString(PrefLastConv, c.id);
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
