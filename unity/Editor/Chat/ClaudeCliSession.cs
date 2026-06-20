using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;

namespace Unimancer
{
    /// <summary>Kind of a streamed chat event surfaced from the headless Claude process.</summary>
    public enum ChatEventKind
    {
        /// <summary>system/init — session established; Text holds the session id.</summary>
        Init,
        /// <summary>An incremental assistant text token (from --include-partial-messages).</summary>
        AssistantDelta,
        /// <summary>The agent invoked a tool; Text holds the tool name.</summary>
        ToolUse,
        /// <summary>Terminal result of the turn; Text holds the final text, IsError set on failure.</summary>
        Result,
        /// <summary>Out-of-band error (process/parse/stderr). Text holds the message.</summary>
        Error,
        /// <summary>A tool returned an image; Text holds the base64 PNG/JPEG data.</summary>
        Image,
        /// <summary>The claude process exited; Text holds a short status.</summary>
        Exit,
        /// <summary>A file Edit/Write/MultiEdit; Text holds a prefixed diff (- removed, + added, § header).</summary>
        Diff,
        /// <summary>Per-turn token/cost usage parsed from the result event (InTok/OutTok/CacheTok/Cost).</summary>
        Usage,
        /// <summary>A subscription rate-limit update (5-hour / weekly window): RlType/RlStatus/RlResetsAt/RlOverage.</summary>
        RateLimit
    }

    /// <summary>One parsed event from the stream-json output, consumed on the main thread.</summary>
    public struct ChatEvent
    {
        public ChatEventKind Kind;
        public string Text;
        public bool IsError;
        public int InTok;      // input tokens (Usage)
        public int OutTok;     // output tokens (Usage)
        public int CacheTok;   // cache read+creation tokens (Usage)
        public double Cost;     // total_cost_usd for the turn (Usage)
        public string RlType;   // rate-limit window: "five_hour" | "seven_day" (RateLimit)
        public string RlStatus; // window status: "allowed" | otherwise constrained (RateLimit)
        public long RlResetsAt; // unix epoch seconds when the window resets (RateLimit)
        public bool RlOverage;  // true if the window is currently drawing on overage (RateLimit)
    }

    /// <summary>
    /// Drives a multi-turn chat with the <c>claude</c> CLI in headless streaming mode,
    /// reusing the user's Claude subscription (NOT a pay-per-token API key).
    ///
    /// Each user turn is one <c>claude -p --output-format stream-json</c> process;
    /// continuity across turns is via <c>--resume &lt;session_id&gt;</c>. The agent loop,
    /// tool dispatch, and MCP-client behaviour all live in Claude Code itself, so the
    /// chat inherits the full Unimancer MCP tool surface for free.
    ///
    /// Subscription auth: we deliberately avoid <c>--bare</c> (which forces an API key)
    /// and <c>unset ANTHROPIC_API_KEY</c> in the shell so Claude Code falls back to the
    /// logged-in subscription / OAuth token.
    ///
    /// Quoting strategy: the MCP config JSON and the user's prompt are written to temp
    /// files and referenced by path, so the WSL command line carries no quotes — this
    /// sidesteps the Windows→WSL double-/single-quote mangling that inline JSON triggers.
    ///
    /// Threading: stdout/stderr arrive on background threads and are pushed onto a
    /// thread-safe queue; the EditorWindow drains <see cref="Events"/> on the main thread.
    /// </summary>
    public class ClaudeCliSession
    {
        /// <summary>Thread-safe queue of parsed events for the UI to drain on the main thread.</summary>
        public readonly ConcurrentQueue<ChatEvent> Events = new ConcurrentQueue<ChatEvent>();

        /// <summary>True while a turn's process is running.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The resolved session id once the first turn establishes one.</summary>
        public string SessionId { get; private set; }

        private readonly bool _wrapWsl;
        private readonly string _claudeCmd;
        private readonly string _nodeServerPath;
        private readonly string _allowedTools;
        private readonly string _model;
        private readonly string _systemPrompt;
        private readonly string _permissionMode;
        private Process _proc;

        // Live sessions so a domain reload can kill their in-flight processes. Without this
        // the claude process (and its child node MCP server) is orphaned on recompile —
        // streaming into a dead handler and keeping port 8090's client side busy. Static
        // state resets each reload, so this never accumulates across reloads.
        private static readonly System.Collections.Generic.List<ClaudeCliSession> Live = new System.Collections.Generic.List<ClaudeCliSession>();

        /// <summary>Register a one-time hook that kills in-flight chat processes before a domain reload.</summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallReloadGuard()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                lock (Live) foreach (var live in Live.ToArray()) { try { live.Cancel(); } catch { } }
            };
        }

        /// <summary>
        /// Create a chat session bound to one Unimancer Node server.
        /// </summary>
        /// <param name="wrapWsl">Run via <c>wsl.exe bash -lc</c> (Unity on Windows, Claude in WSL).</param>
        /// <param name="claudeCmd">The claude executable name/path (default "claude").</param>
        /// <param name="nodeServerPath">Path to Unimancer <c>src/index.js</c> as seen by the shell that runs claude.</param>
        /// <param name="allowedTools">Value for <c>--allowedTools</c> (default allows the unimancer MCP server).</param>
        /// <param name="model">Optional model override; empty = Claude Code default.</param>
        /// <param name="systemPrompt">Extra project context appended to the system prompt (empty = none).</param>
        /// <param name="permissionMode">Claude Code --permission-mode: "acceptEdits" (auto) or "plan" (propose only).</param>
        public ClaudeCliSession(bool wrapWsl, string claudeCmd, string nodeServerPath, string allowedTools, string model, string systemPrompt, string permissionMode)
        {
            _wrapWsl = wrapWsl;
            _claudeCmd = string.IsNullOrEmpty(claudeCmd) ? "claude" : claudeCmd;
            _nodeServerPath = nodeServerPath ?? "";
            // Always permit the built-in Read tool so the agent can view attached
            // screenshots (and read files) — headless -p can't prompt for it otherwise.
            var tools = string.IsNullOrEmpty(allowedTools) ? "mcp__unimancer" : allowedTools;
            if (!tools.Contains("Read")) tools += " Read";
            _allowedTools = tools;
            _model = model ?? "";
            _systemPrompt = systemPrompt ?? "";
            _permissionMode = string.IsNullOrEmpty(permissionMode) ? "acceptEdits" : permissionMode;
            lock (Live) Live.Add(this);
        }

        /// <summary>Forget the conversation so the next <see cref="Send"/> starts fresh.</summary>
        public void Reset()
        {
            SessionId = null;
        }

        /// <summary>Seed a saved session id so the next turn continues that conversation via --resume.</summary>
        public void Resume(string sessionId)
        {
            SessionId = sessionId;
        }

        /// <summary>
        /// Send a user message, starting a streaming turn. No-op if a turn is in flight.
        /// </summary>
        /// <param name="userMessage">The full prompt text (any quotes/newlines are safe — written to a temp file).</param>
        public void Send(string userMessage)
        {
            if (IsBusy) return;
            if (string.IsNullOrEmpty(_nodeServerPath))
            {
                Enqueue(ChatEventKind.Error, "Set the Unimancer src/index.js path in Window → Unimancer → Setup first.", true);
                return;
            }

            try
            {
                // --- Write the MCP config + prompt to temp files (avoids cross-shell quoting). ---
                var tmp = Path.GetTempPath();
                var cfgWin = Path.Combine(tmp, "unimancer-mcp.json");
                var promptWin = Path.Combine(tmp, "unimancer-prompt.txt");
                File.WriteAllText(cfgWin, BuildMcpConfig());
                File.WriteAllText(promptWin, userMessage ?? "");

                var cfgPath = _wrapWsl ? ToWslPath(cfgWin) : cfgWin;
                var promptPath = _wrapWsl ? ToWslPath(promptWin) : promptWin;

                // Optional extra system prompt (e.g. "this project uses Unity VC, not git") via a file.
                string sysPath = null;
                if (!string.IsNullOrEmpty(_systemPrompt))
                {
                    var sysWin = Path.Combine(tmp, "unimancer-system.txt");
                    File.WriteAllText(sysWin, _systemPrompt);
                    sysPath = _wrapWsl ? ToWslPath(sysWin) : sysWin;
                }

                var resumePart = string.IsNullOrEmpty(SessionId) ? "" : $" --resume {SessionId}";
                var modelPart = string.IsNullOrEmpty(_model) ? "" : $" --model {_model}";
                var sysPartWsl = sysPath == null ? "" : $" --append-system-prompt-file {sysPath}";
                var sysPartNative = sysPath == null ? "" : $" --append-system-prompt-file \"{sysPath}\"";

                var psi = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // claude emits UTF-8; without this .NET decodes with the OS codepage and
                    // mangles em-dashes/emoji/arrows (e.g. "—" → "ΓÇö").
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                if (_wrapWsl)
                {
                    // Pipe the prompt from a file; the command line itself stays quote-free.
                    var inner =
                        $"unset ANTHROPIC_API_KEY; cat {promptPath} | {_claudeCmd} -p " +
                        "--output-format stream-json --verbose --include-partial-messages " +
                        $"--mcp-config {cfgPath} --permission-mode {_permissionMode} --allowedTools {_allowedTools}" +
                        resumePart + modelPart + sysPartWsl;
                    psi.FileName = "wsl.exe";
                    psi.Arguments = $"bash -lc \"{inner}\"";
                }
                else
                {
                    psi.FileName = _claudeCmd;
                    psi.Arguments =
                        "-p --output-format stream-json --verbose --include-partial-messages " +
                        $"--mcp-config \"{cfgPath}\" --permission-mode {_permissionMode} --allowedTools {_allowedTools}" +
                        resumePart + modelPart + sysPartNative;
                    psi.EnvironmentVariables.Remove("ANTHROPIC_API_KEY"); // force subscription auth
                }

                _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _proc.OutputDataReceived += (_, e) => { if (e.Data != null) ParseLine(e.Data); };
                _proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Enqueue(ChatEventKind.Error, e.Data, true); };
                _proc.Exited += (_, __) =>
                {
                    IsBusy = false;
                    var code = SafeExitCode();
                    Enqueue(ChatEventKind.Exit, code == 0 ? "done" : $"claude exited with code {code}", code != 0);
                };

                IsBusy = true;
                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();

                if (!_wrapWsl)
                {
                    // Native: feed the prompt over stdin (no shell pipe available).
                    _proc.StandardInput.Write(userMessage ?? "");
                }
                _proc.StandardInput.Close();
            }
            catch (Exception e)
            {
                IsBusy = false;
                Enqueue(ChatEventKind.Error, "Failed to launch claude: " + e.Message, true);
                Debug.LogError("[Unimancer] Chat launch failed: " + e);
            }
        }

        /// <summary>Best-effort kill of the running turn.</summary>
        public void Cancel()
        {
            try { if (_proc != null && !_proc.HasExited) _proc.Kill(); }
            catch { /* already gone */ }
            IsBusy = false;
        }

        /// <summary>Build the inline MCP-server config that points claude at the Unimancer Node server.</summary>
        private string BuildMcpConfig()
        {
            // The shell that runs claude already resolves `node`; the path is shell-native.
            var o = new JObject
            {
                ["mcpServers"] = new JObject
                {
                    ["unimancer"] = new JObject
                    {
                        ["command"] = "node",
                        ["args"] = new JArray { _nodeServerPath }
                    }
                }
            };
            return o.ToString();
        }

        /// <summary>Parse one NDJSON line from stream-json output into a <see cref="ChatEvent"/>.</summary>
        private void ParseLine(string line)
        {
            JObject o;
            try { o = JObject.Parse(line); }
            catch { return; } // non-JSON noise (e.g. a stray log line) — ignore
            var type = (string)o["type"];
            switch (type)
            {
                case "system":
                    if ((string)o["subtype"] == "init")
                    {
                        SessionId = (string)o["session_id"] ?? SessionId;
                        Enqueue(ChatEventKind.Init, SessionId, false);
                    }
                    break;

                case "stream_event":
                    // Incremental text token: event.delta.text on a content_block_delta.
                    var ev = o["event"];
                    if (ev != null && (string)ev["type"] == "content_block_delta")
                    {
                        var delta = ev["delta"];
                        if (delta != null && (string)delta["type"] == "text_delta")
                            Enqueue(ChatEventKind.AssistantDelta, (string)delta["text"] ?? "", false);
                    }
                    break;

                case "assistant":
                    // Surface tool calls; assistant text is rendered via stream_event deltas.
                    var content = o["message"]?["content"] as JArray;
                    if (content != null)
                        foreach (var block in content)
                            if ((string)block["type"] == "tool_use")
                            {
                                Enqueue(ChatEventKind.ToolUse, SummarizeToolUse(block), false);
                                var diff = BuildDiff(block);
                                if (diff != null) Enqueue(ChatEventKind.Diff, diff, false);
                            }
                    ScanForImages(content); // assistant may embed images directly
                    break;

                case "user":
                    // Tool results come back as a user turn; capture any image content.
                    ScanForImages(o["message"]?["content"] as JArray);
                    break;

                case "result":
                    SessionId = (string)o["session_id"] ?? SessionId;
                    var isErr = (bool?)o["is_error"] ?? false;
                    Enqueue(ChatEventKind.Result, (string)o["result"] ?? "", isErr);
                    var usage = o["usage"];
                    Events.Enqueue(new ChatEvent
                    {
                        Kind = ChatEventKind.Usage,
                        InTok = (int?)usage?["input_tokens"] ?? 0,
                        OutTok = (int?)usage?["output_tokens"] ?? 0,
                        CacheTok = ((int?)usage?["cache_read_input_tokens"] ?? 0) + ((int?)usage?["cache_creation_input_tokens"] ?? 0),
                        Cost = (double?)o["total_cost_usd"] ?? 0,
                    });
                    break;

                case "rate_limit_event":
                    // Subscription window status (5-hour / weekly). The headless stream gives
                    // status + reset time per window, not an exact percentage.
                    var rl = o["rate_limit_info"];
                    if (rl != null)
                        Events.Enqueue(new ChatEvent
                        {
                            Kind = ChatEventKind.RateLimit,
                            RlType = (string)rl["rateLimitType"] ?? "",
                            RlStatus = (string)rl["status"] ?? "",
                            RlResetsAt = (long?)rl["resetsAt"] ?? 0,
                            RlOverage = (bool?)rl["isUsingOverage"] ?? false,
                        });
                    break;
            }
        }

        /// <summary>Recursively pull base64 image blocks out of a content array (incl. tool_result).</summary>
        private void ScanForImages(JArray content)
        {
            if (content == null) return;
            foreach (var block in content)
            {
                var t = (string)block["type"];
                if (t == "image")
                {
                    var data = (string)(block["source"]?["data"]);
                    if (!string.IsNullOrEmpty(data)) Enqueue(ChatEventKind.Image, data, false);
                }
                else if (t == "tool_result")
                {
                    ScanForImages(block["content"] as JArray);
                }
            }
        }

        /// <summary>
        /// Build a readable one-liner for a tool_use block so the chat shows *what*
        /// the agent is doing — e.g. "Read …/Chat/UnimancerChatWindow.cs" or
        /// "Bash (npm test)" instead of a bare "Read"/"Bash". Surfaces the most
        /// meaningful argument per tool; falls back to the tool name alone.
        /// </summary>
        private static string SummarizeToolUse(JToken block)
        {
            var name = (string)block["name"] ?? "tool";
            var input = block["input"] as JObject;
            if (input == null) return name;

            // The argument worth showing, in priority order — first present wins.
            string[] keys = { "file_path", "path", "notebook_path", "pattern", "command", "url", "query", "prompt", "description" };
            string arg = null, usedKey = null;
            foreach (var k in keys)
            {
                var v = input[k];
                if (v != null && v.Type != JTokenType.Null && v.Type != JTokenType.Object && v.Type != JTokenType.Array)
                {
                    arg = v.ToString();
                    usedKey = k;
                    break;
                }
            }
            if (string.IsNullOrEmpty(arg)) return name;

            // Paths keep their tail (filename + a little context); free text is length-capped.
            bool isPath = usedKey == "file_path" || usedKey == "path" || usedKey == "notebook_path"
                          || arg.Contains("/") || arg.Contains("\\");
            arg = isPath ? ShortenPath(arg) : Shorten(arg.Replace('\n', ' ').Replace('\r', ' '), 60);
            return name + " " + arg;
        }

        /// <summary>
        /// Build a red/green diff for an Edit/Write/MultiEdit tool call so the chat shows
        /// the actual change (like the normal Claude console), not just "Edit file". Lines
        /// are prefixed: "- " removed, "+ " added, "§ " a file/section header. Capped so a
        /// huge write doesn't flood the transcript. Returns null for non-edit tools.
        /// </summary>
        private static string BuildDiff(JToken block)
        {
            var name = (string)block["name"];
            var input = block["input"] as JObject;
            if (input == null) return null;
            if (name != "Edit" && name != "Write" && name != "MultiEdit") return null;

            var sb = new StringBuilder();
            int count = 0;
            const int MaxLines = 80;
            void AddLines(string text, char sign)
            {
                if (text == null) return;
                foreach (var ln in text.Replace("\r\n", "\n").Split('\n'))
                {
                    if (count >= MaxLines) { sb.Append("§ …(truncated)\n"); return; }
                    sb.Append(sign).Append(' ').Append(ln).Append('\n');
                    count++;
                }
            }

            var fp = ShortenPath((string)input["file_path"] ?? "");
            if (name == "Write")
            {
                sb.Append("§ ").Append(fp).Append("  (write)\n");
                AddLines((string)input["content"], '+');
            }
            else if (name == "MultiEdit")
            {
                sb.Append("§ ").Append(fp).Append("  (multi-edit)\n");
                if (input["edits"] is JArray edits)
                    foreach (var ed in edits)
                    {
                        AddLines((string)ed["old_string"], '-');
                        AddLines((string)ed["new_string"], '+');
                    }
            }
            else // Edit
            {
                sb.Append("§ ").Append(fp).Append('\n');
                AddLines((string)input["old_string"], '-');
                AddLines((string)input["new_string"], '+');
            }
            return sb.ToString();
        }

        /// <summary>Trim a path to its last two segments (e.g. "…/Chat/Foo.cs") for compact display.</summary>
        private static string ShortenPath(string p)
        {
            p = p.Replace('\\', '/').TrimEnd('/');
            var parts = p.Split('/');
            if (parts.Length > 2)
                return "…/" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
            return p;
        }

        /// <summary>Collapse whitespace and cap a string to <paramref name="max"/> chars with an ellipsis.</summary>
        private static string Shorten(string s, int max)
        {
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private int SafeExitCode()
        {
            try { return _proc?.ExitCode ?? -1; } catch { return -1; }
        }

        private void Enqueue(ChatEventKind kind, string text, bool isError)
        {
            Events.Enqueue(new ChatEvent { Kind = kind, Text = text, IsError = isError });
        }

        /// <summary>Translate a Windows path (C:\a\b) to a WSL mount path (/mnt/c/a/b).</summary>
        private static string ToWslPath(string winPath)
        {
            if (string.IsNullOrEmpty(winPath) || winPath.Length < 2 || winPath[1] != ':')
                return winPath;
            var drive = char.ToLowerInvariant(winPath[0]);
            var rest = winPath.Substring(2).Replace('\\', '/');
            return $"/mnt/{drive}{rest}";
        }
    }
}
