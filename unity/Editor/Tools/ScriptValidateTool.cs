using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// SYNTAX-level validation of C# source. Accepts either <c>path</c> (a .cs file
    /// under Assets/ to read) or inline <c>content</c> (exactly one).
    ///
    /// IMPORTANT: this is syntax-level validation only. It checks that the source
    /// parses (balanced braces, well-formed tokens, terminated strings, etc.). It
    /// does NOT perform semantic/type checking — unknown types, missing using
    /// directives, missing assembly references, overload resolution, and the like
    /// are NOT detected here.
    ///
    /// Implementation: Roslyn (Microsoft.CodeAnalysis.CSharp) is not referenced by
    /// this Editor asmdef and may not be loaded in every project, so we do NOT take
    /// a compile-time dependency on it. Instead we attempt to locate and invoke
    /// CSharpSyntaxTree.ParseText via REFLECTION at runtime. If the Roslyn
    /// assemblies are not present/loadable, we FALL BACK to a reduced structural
    /// balance check and set a <c>note</c> on the result so callers know the check
    /// was downgraded. Runs synchronously.
    /// </summary>
    public class ScriptValidateTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_validate";

        /// <inheritdoc />
        public override string Description =>
            "Syntax-validate C# source (path under Assets/ OR inline content). Returns { ok, errorCount, diagnostics }. Syntax-level only (no semantic/type checking); falls back to a reduced balance check if Roslyn is unavailable.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Resolve the source text, then validate it.
        /// </summary>
        /// <param name="parameters">Exactly one of path (under Assets/) or content (inline).</param>
        /// <returns>{ ok, errorCount, diagnostics:[{severity,line,column,message}], note? }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var pathToken = parameters["path"];
                var contentToken = parameters["content"];

                var hasPath = pathToken != null && pathToken.Type != JTokenType.Null && !string.IsNullOrEmpty(pathToken.ToString());
                var hasContent = contentToken != null && contentToken.Type != JTokenType.Null;

                // Enforce "exactly one of path / content".
                if (hasPath == hasContent)
                    return new JObject { ["error"] = "provide exactly one of path (file under Assets/) or content (inline source)" };

                string source;
                if (hasPath)
                {
                    var path = pathToken.ToString();
                    var guard = PathGuard.Validate(path);
                    if (guard != null) return guard;
                    if (!File.Exists(path))
                        return new JObject { ["error"] = $"file not found: {path}" };
                    source = File.ReadAllText(path);
                }
                else
                {
                    source = contentToken.ToString();
                }

                // Prefer Roslyn (via reflection); fall back to the balance check.
                JObject roslyn = TryRoslyn(source);
                if (roslyn != null)
                    return roslyn;

                return FallbackBalanceCheck(source);
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Attempt to parse <paramref name="source"/> with Roslyn's
        /// CSharpSyntaxTree.ParseText via reflection and collect its SYNTAX
        /// diagnostics. Returns null if Roslyn cannot be located/invoked, signaling
        /// the caller to fall back.
        /// </summary>
        /// <param name="source">C# source text.</param>
        /// <returns>Result JObject from Roslyn, or null if Roslyn is unavailable.</returns>
        private static JObject TryRoslyn(string source)
        {
            try
            {
                // CSharpSyntaxTree lives in Microsoft.CodeAnalysis.CSharp.
                Type syntaxTreeType =
                    Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree, Microsoft.CodeAnalysis.CSharp")
                    ?? FindLoadedType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
                if (syntaxTreeType == null)
                    return null;

                // SyntaxTree CSharpSyntaxTree.ParseText(string text, ...) — pick the
                // overload whose first parameter is a string and invoke with defaults
                // for the rest (options/path/encoding/cancellationToken).
                MethodInfo parseText = syntaxTreeType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "ParseText")
                    .FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length >= 1 && ps[0].ParameterType == typeof(string);
                    });
                if (parseText == null)
                    return null;

                var ps2 = parseText.GetParameters();
                var args = new object[ps2.Length];
                args[0] = source;
                for (int i = 1; i < ps2.Length; i++)
                    args[i] = ps2[i].HasDefaultValue ? ps2[i].DefaultValue : DefaultFor(ps2[i].ParameterType);

                object tree = parseText.Invoke(null, args);
                if (tree == null)
                    return null;

                // IEnumerable<Diagnostic> tree.GetDiagnostics() — the no-arg overload
                // returns syntax diagnostics for the whole tree.
                MethodInfo getDiagnostics = tree.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetDiagnostics" && m.GetParameters().Length == 0);
                if (getDiagnostics == null)
                    return null;

                var diagsEnumerable = getDiagnostics.Invoke(tree, null) as IEnumerable;
                if (diagsEnumerable == null)
                    return null;

                var diagnostics = new JArray();
                int errorCount = 0;

                foreach (var diag in diagsEnumerable)
                {
                    if (diag == null) continue;
                    var dt = diag.GetType();

                    // Diagnostic.Severity -> enum DiagnosticSeverity (Hidden/Info/Warning/Error).
                    string severity = dt.GetProperty("Severity")?.GetValue(diag)?.ToString() ?? "Unknown";
                    if (string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase))
                        errorCount++;

                    // Diagnostic.GetMessage() -> string.
                    string message = dt.GetMethod("GetMessage", Type.EmptyTypes)?.Invoke(diag, null) as string
                                     ?? diag.ToString();

                    // Diagnostic.Location.GetLineSpan().StartLinePosition.{Line,Character} (0-based).
                    int line = 0, column = 0;
                    var location = dt.GetProperty("Location")?.GetValue(diag);
                    if (location != null)
                    {
                        var lineSpan = location.GetType().GetMethod("GetLineSpan", Type.EmptyTypes)?.Invoke(location, null);
                        if (lineSpan != null)
                        {
                            var startPos = lineSpan.GetType().GetProperty("StartLinePosition")?.GetValue(lineSpan);
                            if (startPos != null)
                            {
                                var lineProp = startPos.GetType().GetProperty("Line")?.GetValue(startPos);
                                var charProp = startPos.GetType().GetProperty("Character")?.GetValue(startPos);
                                if (lineProp is int lp) line = lp + 1;   // report 1-based
                                if (charProp is int cp) column = cp + 1; // report 1-based
                            }
                        }
                    }

                    diagnostics.Add(new JObject
                    {
                        ["severity"] = severity,
                        ["line"] = line,
                        ["column"] = column,
                        ["message"] = message,
                    });
                }

                return new JObject
                {
                    ["ok"] = errorCount == 0,
                    ["errorCount"] = errorCount,
                    ["diagnostics"] = diagnostics,
                };
            }
            catch
            {
                // Any reflection/runtime failure -> let the caller fall back.
                return null;
            }
        }

        /// <summary>
        /// Search already-loaded assemblies for a type by full name (handles Roslyn
        /// assemblies that are loaded but not resolvable by Type.GetType's name).
        /// </summary>
        /// <param name="fullName">Fully-qualified type name.</param>
        /// <returns>The Type, or null if not found.</returns>
        private static Type FindLoadedType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName, false); }
                catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Default value for a value type, or null for reference types.</summary>
        /// <param name="t">The parameter type.</param>
        /// <returns>A safe default to pass via reflection.</returns>
        private static object DefaultFor(Type t)
        {
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }

        /// <summary>
        /// Reduced structural check used when Roslyn is unavailable: verifies that
        /// braces, parentheses, and brackets are balanced and that string/char
        /// literals are terminated. This is a coarse heuristic — it is NOT a real
        /// parser and will miss many real syntax errors — hence the <c>note</c>.
        /// </summary>
        /// <param name="source">C# source text.</param>
        /// <returns>{ ok, errorCount, diagnostics, note }.</returns>
        private static JObject FallbackBalanceCheck(string source)
        {
            var diagnostics = new JArray();

            int line = 1, column = 0;
            var stack = new Stack<KeyValuePair<char, int[]>>(); // bracket char + [line,col]

            // String/char/comment state so brackets inside literals/comments are ignored.
            bool inString = false, inChar = false, inVerbatim = false;
            bool inLineComment = false, inBlockComment = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (c == '\n') { line++; column = 0; inLineComment = false; continue; }
                column++;

                if (inLineComment) continue;

                if (inBlockComment)
                {
                    if (c == '*' && next == '/') { inBlockComment = false; i++; column++; }
                    continue;
                }

                if (inString)
                {
                    if (inVerbatim)
                    {
                        if (c == '"' && next == '"') { i++; column++; }       // escaped "" in verbatim
                        else if (c == '"') { inString = false; inVerbatim = false; }
                    }
                    else
                    {
                        if (c == '\\') { i++; column++; }                      // skip escaped char
                        else if (c == '"') inString = false;
                        else if (c == '\n') { /* unterminated handled below */ }
                    }
                    continue;
                }

                if (inChar)
                {
                    if (c == '\\') { i++; column++; }
                    else if (c == '\'') inChar = false;
                    continue;
                }

                // Not currently inside a literal/comment.
                if (c == '/' && next == '/') { inLineComment = true; i++; column++; continue; }
                if (c == '/' && next == '*') { inBlockComment = true; i++; column++; continue; }
                if (c == '@' && next == '"') { inString = true; inVerbatim = true; i++; column++; continue; }
                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }

                if (c == '{' || c == '(' || c == '[')
                {
                    stack.Push(new KeyValuePair<char, int[]>(c, new[] { line, column }));
                }
                else if (c == '}' || c == ')' || c == ']')
                {
                    char open = c == '}' ? '{' : c == ')' ? '(' : '[';
                    if (stack.Count == 0 || stack.Peek().Key != open)
                    {
                        diagnostics.Add(new JObject
                        {
                            ["severity"] = "Error",
                            ["line"] = line,
                            ["column"] = column,
                            ["message"] = $"unmatched closing '{c}'",
                        });
                    }
                    else
                    {
                        stack.Pop();
                    }
                }
            }

            // Any still-open brackets are unbalanced.
            foreach (var open in stack)
            {
                diagnostics.Add(new JObject
                {
                    ["severity"] = "Error",
                    ["line"] = open.Value[0],
                    ["column"] = open.Value[1],
                    ["message"] = $"unmatched opening '{open.Key}'",
                });
            }

            if (inString)
                diagnostics.Add(new JObject { ["severity"] = "Error", ["line"] = line, ["message"] = "unterminated string literal" });
            if (inChar)
                diagnostics.Add(new JObject { ["severity"] = "Error", ["line"] = line, ["message"] = "unterminated character literal" });
            if (inBlockComment)
                diagnostics.Add(new JObject { ["severity"] = "Error", ["line"] = line, ["message"] = "unterminated block comment" });

            int errorCount = diagnostics.Count;

            return new JObject
            {
                ["ok"] = errorCount == 0,
                ["errorCount"] = errorCount,
                ["diagnostics"] = diagnostics,
                ["note"] = "Roslyn unavailable; reduced check only (balanced braces/parens/brackets + terminated string/char/comment). NOT a full parser and NOT semantic/type checking.",
            };
        }
    }
}
