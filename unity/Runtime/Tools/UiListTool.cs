using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// List interactable uGUI elements (Selectables: Button, Toggle, Slider, …) in
    /// the running scene so an agent can discover what it can click/interact with via
    /// <see cref="UiClickTool"/>. Reflection-based to avoid a hard uGUI dependency.
    /// </summary>
    public class UiListTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_ui_list";

        /// <inheritdoc/>
        public override string Description =>
            "List interactable uGUI elements (Button/Toggle/Slider/…) in the RUNNING scene so you know what runtime_ui_click can target. Returns { count, elements:[{ name, path, type, interactable, active }] }.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var selType = RtResolve.ResolveType("UnityEngine.UI.Selectable");
                if (selType == null)
                    return new JObject { ["error"] = "UnityEngine.UI.Selectable not found (is com.unity.ugui present?)" };

                // Two-arg overload (no FindObjectsSortMode) — deprecated in Unity 6 (CS0618).
                var found = UnityEngine.Object.FindObjectsByType(selType, FindObjectsInactive.Include);
                var interactableProp = selType.GetProperty("interactable", BindingFlags.Public | BindingFlags.Instance);

                var elements = new JArray();
                foreach (var obj in found)
                {
                    if (!(obj is Component comp)) continue;
                    bool interactable = interactableProp != null && (bool)interactableProp.GetValue(comp);
                    elements.Add(new JObject
                    {
                        ["name"] = comp.gameObject.name,
                        ["path"] = RtResolve.Path(comp.gameObject),
                        ["type"] = comp.GetType().Name,
                        ["interactable"] = interactable,
                        ["active"] = comp.gameObject.activeInHierarchy,
                    });
                }
                return new JObject { ["count"] = elements.Count, ["elements"] = elements };
            }
            catch (Exception e) { return new JObject { ["error"] = e.Message }; }
        }
    }
}
