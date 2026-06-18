using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Looks up a shader by name and reports its properties, or — when no name is
    /// given — lists the available shaders in the project. Output is capped to keep
    /// responses manageable. Read-only; runs synchronously.
    /// </summary>
    public class ShaderFindTool : McpToolBase
    {
        /// <summary>Max number of shader names returned when listing.</summary>
        private const int MaxShaders = 300;

        /// <inheritdoc />
        public override string Name => "shader_find";

        /// <inheritdoc />
        public override string Description =>
            "Look up a shader by name (returns its properties) or list available shaders when no name is given.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Find a shader or enumerate shaders.
        /// </summary>
        /// <param name="parameters">name (optional).</param>
        /// <returns>{ found, name, properties } for a lookup, or { shaders, count, truncated } for a listing.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var name = parameters["name"]?.ToString();

                if (!string.IsNullOrEmpty(name))
                {
                    var shader = Shader.Find(name);
                    if (shader == null)
                        return new JObject { ["found"] = false, ["name"] = name };

                    var props = new JArray();
                    var count = ShaderUtil.GetPropertyCount(shader);
                    for (var i = 0; i < count; i++)
                    {
                        props.Add(new JObject
                        {
                            ["name"] = ShaderUtil.GetPropertyName(shader, i),
                            ["type"] = ShaderUtil.GetPropertyType(shader, i).ToString(),
                            ["description"] = ShaderUtil.GetPropertyDescription(shader, i),
                        });
                    }

                    return new JObject
                    {
                        ["found"] = true,
                        ["name"] = shader.name,
                        ["properties"] = props,
                    };
                }

                // No name: list available shaders (capped).
                var infos = ShaderUtil.GetAllShaderInfo();
                var total = infos.Length;
                var names = new JArray();
                foreach (var info in infos.Take(MaxShaders))
                    names.Add(info.name);

                return new JObject
                {
                    ["shaders"] = names,
                    ["count"] = total,
                    ["truncated"] = total > MaxShaders,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
