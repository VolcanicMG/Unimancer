using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Sets shader properties on an existing Material, inferring each property's
    /// type from its value:
    ///   - array length 4 -> Color (RGBA)
    ///   - array length 2 or 3 -> Vector
    ///   - number -> Float
    ///   - string that resolves to a loadable Texture asset -> Texture
    /// Marks the material dirty and saves. Runs synchronously.
    /// </summary>
    public class MaterialSetPropertiesTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "material_set_properties";

        /// <inheritdoc />
        public override string Description =>
            "Set shader properties on a Material; type inferred from value (color/vector/float/texture).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Apply each property and save the asset.
        /// </summary>
        /// <param name="parameters">path (required, Assets/...mat), properties (required object).</param>
        /// <returns>{ applied: [keys], skipped: [{key,reason}] }, or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new JObject { ["error"] = "path is required" };

                // Path-safety guard: confine the target material to the project's Assets/ tree.
                var guard = PathGuard.Validate(path);
                if (guard != null) return guard;

                if (!(parameters["properties"] is JObject properties))
                    return new JObject { ["error"] = "properties (object) is required" };

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    return new JObject { ["error"] = $"material not found at: {path}" };

                var applied = new JArray();
                var skipped = new JArray();

                foreach (var prop in properties)
                {
                    var key = prop.Key;
                    var value = prop.Value;

                    try
                    {
                        if (value is JArray arr)
                        {
                            var nums = arr.Select(t => t.ToObject<float>()).ToArray();
                            if (nums.Length == 4)
                            {
                                // Length 4 -> treat as Color (RGBA).
                                material.SetColor(key, new Color(nums[0], nums[1], nums[2], nums[3]));
                            }
                            else if (nums.Length == 3)
                            {
                                material.SetVector(key, new Vector4(nums[0], nums[1], nums[2], 0f));
                            }
                            else if (nums.Length == 2)
                            {
                                material.SetVector(key, new Vector4(nums[0], nums[1], 0f, 0f));
                            }
                            else
                            {
                                skipped.Add(new JObject { ["key"] = key, ["reason"] = $"unsupported array length {nums.Length}" });
                                continue;
                            }
                        }
                        else if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                        {
                            material.SetFloat(key, value.ToObject<float>());
                        }
                        else if (value.Type == JTokenType.String)
                        {
                            // String -> attempt to load as a Texture asset path.
                            var assetPath = value.ToString();
                            var tex = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                            if (tex == null)
                            {
                                skipped.Add(new JObject { ["key"] = key, ["reason"] = $"no Texture at '{assetPath}'" });
                                continue;
                            }
                            material.SetTexture(key, tex);
                        }
                        else
                        {
                            skipped.Add(new JObject { ["key"] = key, ["reason"] = $"unsupported value type {value.Type}" });
                            continue;
                        }

                        applied.Add(key);
                    }
                    catch (Exception inner)
                    {
                        skipped.Add(new JObject { ["key"] = key, ["reason"] = inner.Message });
                    }
                }

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return new JObject { ["applied"] = applied, ["skipped"] = skipped };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
