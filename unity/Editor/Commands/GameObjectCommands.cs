using System;
using System.Collections.Generic;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Unimancer.Commands
{
    /// <summary>
    /// GameObject inspection commands contributed to com.unity.pipeline.
    /// Deliberately thin: the built-in <c>get_component_properties</c> covers the
    /// serialized-property surface, so this only answers "what is on this object".
    /// </summary>
    public static class GameObjectCommands
    {
        /// <summary>
        /// List the components on a GameObject with their type, enabled state and
        /// handle. A missing/broken script slot surfaces as a null component and is
        /// reported as <c>&lt;missing script&gt;</c>.
        /// </summary>
        [CliCommand("component_list",
            "List a GameObject's components: type, enabled state and instanceID. " +
            "Use the built-in get_component_properties to read a component's serialized properties. " +
            "Returns {path, components:[{type, enabled, instanceID}]}.",
            Tags = new[] { "gameobjects" })]
        public static Dictionary<string, object> ComponentList(
            [CliArg("target", "Hierarchy path or instanceID of the GameObject.", Required = true)] string target,
            [CliArg("includeInactive", "Include disabled components (default true).")] bool includeInactive = true)
        {
            var go = GoResolve.Resolve(target);
            if (go == null)
                throw new ArgumentException($"GameObject not found: {target}");

            var components = new List<object>();
            foreach (var comp in go.GetComponents<Component>())
            {
                // A missing/broken script slot surfaces as a null component.
                if (comp == null)
                {
                    components.Add(new Dictionary<string, object> { ["type"] = "<missing script>" });
                    continue;
                }

                // Only Behaviour carries an enabled flag; everything else is always on.
                bool enabled = comp is Behaviour b ? b.enabled : true;
                if (!includeInactive && !enabled)
                    continue;

                components.Add(new Dictionary<string, object>
                {
                    ["type"] = comp.GetType().FullName,
                    ["enabled"] = enabled,
                    ["instanceID"] = EntityId.ToULong(comp.GetEntityId()).ToString(),
                });
            }

            return new Dictionary<string, object>
            {
                ["path"] = GoResolve.Path(go),
                ["components"] = components,
            };
        }
    }
}
