using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Sets a public field or property on a component of a running GameObject,
    /// coercing the incoming JSON value to the member's type (int/float/bool/string/
    /// enum/Vector3). Live-tweak tool for driving game state.
    /// </summary>
    public class SetComponentPropertyTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_set_component_property";

        /// <inheritdoc/>
        public override string Description =>
            "Set a public field/property on a component of a running GameObject. params: target, componentType, member, value. Coerces value to the member type (int/float/bool/string/enum/Vector3 via {x,y,z}).";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var member = parameters["member"]?.ToString();
                var value = parameters["value"];

                var go = RtResolve.Resolve(target);
                if (go == null) return new JObject { ["error"] = $"GameObject not found: {target}" };

                var type = RtResolve.ResolveType(componentType);
                if (type == null) return new JObject { ["error"] = $"Type not found: {componentType}" };

                var comp = go.GetComponent(type);
                if (comp == null)
                    return new JObject { ["error"] = $"Component {componentType} not on {target}" };

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                                           BindingFlags.FlattenHierarchy;

                var field = type.GetField(member, flags);
                if (field != null)
                {
                    field.SetValue(comp, Coerce(value, field.FieldType));
                    return new JObject { ["set"] = true, ["member"] = member, ["type"] = comp.GetType().FullName };
                }

                var prop = type.GetProperty(member, flags);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(comp, Coerce(value, prop.PropertyType));
                    return new JObject { ["set"] = true, ["member"] = member, ["type"] = comp.GetType().FullName };
                }

                return new JObject { ["error"] = $"No writable public field/property '{member}' on {componentType}" };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>
        /// Coerce a JSON token to a target CLR type. Handles primitives, strings,
        /// enums (by name or numeric value), and Vector3 from {x,y,z}; otherwise
        /// falls back to JToken.ToObject.
        /// </summary>
        /// <param name="value">Incoming JSON value.</param>
        /// <param name="t">Target member type.</param>
        /// <returns>The converted value, assignable to <paramref name="t"/>.</returns>
        internal static object Coerce(JToken value, Type t)
        {
            if (value == null) return null;

            if (t == typeof(int)) return value.ToObject<int>();
            if (t == typeof(float)) return value.ToObject<float>();
            if (t == typeof(double)) return value.ToObject<double>();
            if (t == typeof(bool)) return value.ToObject<bool>();
            if (t == typeof(string)) return value.ToString();

            if (t.IsEnum)
                return value.Type == JTokenType.Integer
                    ? Enum.ToObject(t, value.ToObject<int>())
                    : Enum.Parse(t, value.ToString(), true);

            if (t == typeof(Vector3))
            {
                var o = value as JObject;
                if (o != null)
                    return new Vector3(
                        o["x"]?.ToObject<float>() ?? 0f,
                        o["y"]?.ToObject<float>() ?? 0f,
                        o["z"]?.ToObject<float>() ?? 0f);
            }

            return value.ToObject(t);
        }
    }
}
