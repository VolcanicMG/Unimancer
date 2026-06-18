using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Unimancer
{
    /// <summary>
    /// Returns the SHA-256 (hex) of a C# file's exact UTF-8 bytes plus its line
    /// count. An editing client can capture this fingerprint and later pass it to
    /// <c>script_apply_edits</c> as <c>expectedSha</c> to confirm the file has not
    /// changed before writing. Read-only; runs synchronously.
    /// </summary>
    public class ScriptGetShaTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "script_get_sha";

        /// <inheritdoc />
        public override string Description =>
            "Return the SHA-256 (hex) of a .cs file's UTF-8 bytes plus its line count, for change detection before edits.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Compute the SHA-256 and line count of the file at the given path.
        /// </summary>
        /// <param name="parameters">path (required, a .cs file under Assets/).</param>
        /// <returns>{ path, sha256, lines }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: confine reads to the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    return new JObject { ["error"] = $"path must be a .cs file: {path}" };

                if (!File.Exists(path))
                    return new JObject { ["error"] = $"file not found: {path}" };

                // Hash the exact on-disk bytes so the fingerprint matches the file
                // independent of how we later re-read/encode it.
                var bytes = File.ReadAllBytes(path);
                string hex = ScriptHash.Sha256Hex(bytes);

                // Decode as UTF-8 to count lines consistently with how we edit.
                var text = Encoding.UTF8.GetString(bytes);
                int lines = ScriptHash.CountLines(text);

                return new JObject
                {
                    ["path"] = path,
                    ["sha256"] = hex,
                    ["lines"] = lines,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }

    /// <summary>
    /// Shared hashing / line-counting helpers for the script-editing tools, so the
    /// sha fingerprint produced by <c>script_get_sha</c> and verified by
    /// <c>script_apply_edits</c> is computed identically.
    /// </summary>
    public static class ScriptHash
    {
        /// <summary>Compute the lowercase hex SHA-256 of the given bytes.</summary>
        /// <param name="bytes">Raw bytes to hash.</param>
        /// <returns>64-char lowercase hex string.</returns>
        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Compute the lowercase hex SHA-256 of a string's UTF-8 bytes.</summary>
        /// <param name="text">Text to hash.</param>
        /// <returns>64-char lowercase hex string.</returns>
        public static string Sha256HexOf(string text)
        {
            return Sha256Hex(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// Count the number of lines in the text. An empty string is 0 lines; any
        /// non-empty text has at least 1 line, plus one extra per newline (a
        /// trailing newline counts the final empty line, matching typical editors).
        /// </summary>
        /// <param name="text">Text to count.</param>
        /// <returns>Line count.</returns>
        public static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            int count = 1;
            foreach (var c in text)
                if (c == '\n') count++;
            return count;
        }
    }
}
