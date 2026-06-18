using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Set a single serialized property on a component, coercing the supplied
    /// JSON value to the property's SerializedPropertyType. Recorded for Undo via
    /// SerializedObject.ApplyModifiedProperties.
    /// </summary>
    public class ComponentSetPropertyTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "component_set_property";

        /// <inheritdoc />
        public override string Description =>
            "Set one serialized property on a component; returns {set, type, value}.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve target/component/property and write the coerced value.</summary>
        /// <param name="parameters">target, componentType, propertyPath, value (all required).</param>
        /// <returns>{set, type, value} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var type = ComponentTypeResolve.Resolve(parameters["componentType"]?.ToString());
                if (type == null)
                    return new JObject { ["error"] = $"component type not found: {parameters["componentType"]}" };

                var comp = go.GetComponent(type);
                if (comp == null)
                    return new JObject { ["error"] = $"no {type.Name} on {GoResolve.Path(go)}" };

                var propertyPath = parameters["propertyPath"]?.ToString();
                if (string.IsNullOrEmpty(propertyPath))
                    return new JObject { ["error"] = "propertyPath is required" };

                var value = parameters["value"];

                var so = new SerializedObject(comp);
                var prop = so.FindProperty(propertyPath);
                if (prop == null)
                    return new JObject { ["error"] = $"property not found: {propertyPath}" };

                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = (int)value; break;
                    case SerializedPropertyType.Float:
                        prop.floatValue = (float)value; break;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = (bool)value; break;
                    case SerializedPropertyType.String:
                        prop.stringValue = value?.ToString() ?? ""; break;
                    case SerializedPropertyType.Enum:
                        // Accept an integer index or an enum name string.
                        if (value != null && value.Type == JTokenType.String)
                        {
                            var idx = Array.IndexOf(prop.enumNames, value.ToString());
                            if (idx < 0) return new JObject { ["error"] = $"enum value not found: {value}" };
                            prop.enumValueIndex = idx;
                        }
                        else { prop.enumValueIndex = (int)value; }
                        break;
                    case SerializedPropertyType.Vector2:
                        prop.vector2Value = new Vector2((float)value["x"], (float)value["y"]); break;
                    case SerializedPropertyType.Vector3:
                        prop.vector3Value = new Vector3((float)value["x"], (float)value["y"], (float)value["z"]); break;
                    case SerializedPropertyType.Vector4:
                        prop.vector4Value = new Vector4((float)value["x"], (float)value["y"], (float)value["z"], (float)value["w"]); break;
                    case SerializedPropertyType.Color:
                        prop.colorValue = new Color(
                            (float)value["r"], (float)value["g"], (float)value["b"],
                            value["a"] != null ? (float)value["a"] : 1f);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        // Treat the value as an asset path; null/empty clears the ref.
                        var path = value?.ToString();
                        if (string.IsNullOrEmpty(path)) { prop.objectReferenceValue = null; }
                        else
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (asset == null) return new JObject { ["error"] = $"asset not found at: {path}" };
                            prop.objectReferenceValue = asset;
                        }
                        break;
                    default:
                        return new JObject { ["error"] = $"unsupported property type: {prop.propertyType}" };
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(comp);

                return new JObject
                {
                    ["set"] = propertyPath,
                    ["type"] = prop.propertyType.ToString(),
                    ["value"] = value,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
