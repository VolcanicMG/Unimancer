using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// List every component on a GameObject along with its serialized properties,
    /// read by iterating a SerializedObject over each component.
    /// </summary>
    public class ComponentListTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "component_list";

        /// <inheritdoc />
        public override string Description =>
            "List a GameObject's components and serialized properties; returns {path, components:[...]}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the target and dump components + their properties.</summary>
        /// <param name="parameters">target (required).</param>
        /// <returns>{path, components:[{type, properties:[{propertyPath, value}]}]} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var components = new JArray();
                foreach (var comp in go.GetComponents<Component>())
                {
                    // A missing/broken script slot surfaces as a null component.
                    if (comp == null)
                    {
                        components.Add(new JObject { ["type"] = "<missing script>" });
                        continue;
                    }

                    var props = new JArray();
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    // enterChildren=true on the first call descends into the root.
                    var enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false; // only descend the first level automatically
                        props.Add(new JObject
                        {
                            ["propertyPath"] = it.propertyPath,
                            ["type"] = it.propertyType.ToString(),
                            ["value"] = Stringify(it),
                        });
                    }

                    components.Add(new JObject
                    {
                        ["type"] = comp.GetType().FullName,
                        ["properties"] = props,
                    });
                }

                return new JObject { ["path"] = GoResolve.Path(go), ["components"] = components };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>Produce a readable string for a SerializedProperty's value.</summary>
        /// <param name="p">The property to stringify.</param>
        /// <returns>A string form of the value, by property type.</returns>
        private static string Stringify(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.floatValue.ToString();
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length
                        ? p.enumNames[p.enumValueIndex]
                        : p.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return p.vector4Value.ToString();
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return p.objectReferenceValue != null ? p.objectReferenceValue.name : "null";
                case SerializedPropertyType.Bounds: return p.boundsValue.ToString();
                case SerializedPropertyType.Rect: return p.rectValue.ToString();
                case SerializedPropertyType.Quaternion: return p.quaternionValue.ToString();
                default: return p.propertyType.ToString();
            }
        }
    }
}
