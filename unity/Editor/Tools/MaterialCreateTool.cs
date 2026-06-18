using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Creates a new Material asset under Assets/ with a chosen shader. Defaults to
    /// the URP Lit shader and falls back to Standard if that shader is not present
    /// in the project (e.g. a non-URP project). Runs synchronously.
    /// </summary>
    public class MaterialCreateTool : McpToolBase
    {
        /// <summary>Default shader for new materials (URP).</summary>
        private const string DefaultShader = "Universal Render Pipeline/Lit";

        /// <summary>Fallback shader when the requested/default shader is unavailable.</summary>
        private const string FallbackShader = "Standard";

        /// <inheritdoc />
        public override string Name => "material_create";

        /// <inheritdoc />
        public override string Description =>
            "Create a new Material under Assets/ (path ending in .mat) using the given shader (default URP Lit, falling back to Standard).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Create and save the material asset.
        /// </summary>
        /// <param name="parameters">path (required, Assets/...mat), shader (optional).</param>
        /// <returns>{ created, shader, note? }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: keep the new asset inside the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!path.EndsWith(".mat", StringComparison.Ordinal))
                    return new JObject { ["error"] = "path must end in .mat" };

                var shaderName = parameters["shader"]?.ToString();
                if (string.IsNullOrEmpty(shaderName)) shaderName = DefaultShader;

                string note = null;
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    // Requested/default shader not found — fall back and note it.
                    note = $"shader '{shaderName}' not found; fell back to '{FallbackShader}'";
                    shader = Shader.Find(FallbackShader);
                    shaderName = FallbackShader;
                    if (shader == null)
                        return new JObject { ["error"] = $"neither requested shader nor '{FallbackShader}' was found" };
                }

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssets();

                var result = new JObject
                {
                    ["created"] = path,
                    ["shader"] = shaderName,
                };
                if (note != null) result["note"] = note;
                return result;
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
