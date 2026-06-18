using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// THE powerful "drive the game" tool: reflectively invokes a public instance
    /// method on a component of a running GameObject, coercing each JSON arg to its
    /// parameter type. Use it to trigger gameplay actions (Jump, TakeDamage,
    /// LoadLevel, etc.) live, without rebuilding.
    /// </summary>
    public class CallMethodTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_call_method";

        /// <inheritdoc/>
        public override string Description =>
            "Drive the running game: invoke a public instance method on a component. params: target, componentType, method, args? (array). Coerces args to parameter types. Returns { called, result }. Powerful — can mutate live game state.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var methodName = parameters["method"]?.ToString();
                var argsArray = parameters["args"] as JArray ?? new JArray();

                var go = RtResolve.Resolve(target);
                if (go == null) return new JObject { ["error"] = $"GameObject not found: {target}" };

                var type = RtResolve.ResolveType(componentType);
                if (type == null) return new JObject { ["error"] = $"Type not found: {componentType}" };

                var comp = go.GetComponent(type);
                if (comp == null)
                    return new JObject { ["error"] = $"Component {componentType} not on {target}" };

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                                           BindingFlags.FlattenHierarchy;

                // Match a public instance method by name AND argument count.
                var method = type.GetMethods(flags)
                    .FirstOrDefault(m => m.Name == methodName &&
                                         m.GetParameters().Length == argsArray.Count);
                if (method == null)
                    return new JObject
                    {
                        ["error"] = $"No public method '{methodName}' with {argsArray.Count} arg(s) on {componentType}"
                    };

                var pInfos = method.GetParameters();
                var callArgs = new object[pInfos.Length];
                for (int i = 0; i < pInfos.Length; i++)
                    callArgs[i] = SetComponentPropertyTool.Coerce(argsArray[i], pInfos[i].ParameterType);

                var ret = method.Invoke(comp, callArgs);

                JToken resultToken;
                if (ret == null)
                {
                    resultToken = JValue.CreateNull();
                }
                else
                {
                    // Prefer a structured JSON view; fall back to ToString for types
                    // Json.NET can't serialize cleanly.
                    try { resultToken = JToken.FromObject(ret); }
                    catch { resultToken = new JValue(ret.ToString()); }
                }

                return new JObject { ["called"] = true, ["result"] = resultToken };
            }
            catch (System.Exception e)
            {
                // Unwrap reflection's wrapper so callers see the real exception.
                var msg = e is TargetInvocationException tie && tie.InnerException != null
                    ? tie.InnerException.Message
                    : e.Message;
                return new JObject { ["error"] = msg };
            }
        }
    }
}
