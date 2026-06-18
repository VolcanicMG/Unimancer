using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Dumps the serialized state of a component on a target GameObject in the
    /// RUNNING game, using JsonUtility (so it shows the same fields the Inspector
    /// serializes). If several components of the type exist, the first is used.
    /// </summary>
    public class GetComponentTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_get_component";

        /// <inheritdoc/>
        public override string Description =>
            "Read a component's serialized fields on a running GameObject. params: target (name/path), componentType. Returns { type, json } (JsonUtility dump of the first matching component).";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();

                var go = RtResolve.Resolve(target);
                if (go == null) return new JObject { ["error"] = $"GameObject not found: {target}" };

                var type = RtResolve.ResolveType(componentType);
                if (type == null) return new JObject { ["error"] = $"Type not found: {componentType}" };

                var comp = go.GetComponent(type);
                if (comp == null)
                    return new JObject { ["error"] = $"Component {componentType} not on {target}" };

                return new JObject
                {
                    ["type"] = comp.GetType().FullName,
                    ["json"] = JsonUtility.ToJson(comp, true),
                };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
