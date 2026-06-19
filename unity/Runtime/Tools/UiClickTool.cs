using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Simulate a UI click on a running GameObject by dispatching pointer
    /// enter/down/up/click events to its uGUI handlers (Button → onClick, Toggle,
    /// EventTrigger, or any custom IPointerClickHandler). Done via reflection on
    /// UnityEngine.EventSystems so the Runtime assembly keeps ZERO hard dependency
    /// on com.unity.ugui (it still compiles in projects without uGUI; the tool just
    /// reports the types are missing at call time).
    /// </summary>
    public class UiClickTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_ui_click";

        /// <inheritdoc/>
        public override string Description =>
            "Click a UI element in the RUNNING game (Play mode/dev build) to test interactions. params: target (GameObject name or hierarchy path). Dispatches pointer down/up/click to the target's uGUI handlers (Button onClick, Toggle, EventTrigger, IPointerClickHandler). Returns { clicked, target, handlers }. Use runtime_ui_list to discover targets.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = RtResolve.Resolve(target);
                if (go == null) return new JObject { ["error"] = $"GameObject not found: {target}" };
                if (!go.activeInHierarchy) return new JObject { ["error"] = $"GameObject is inactive (can't click): {target}" };

                var handlerType = RtResolve.ResolveType("UnityEngine.EventSystems.IPointerClickHandler");
                var pedType = RtResolve.ResolveType("UnityEngine.EventSystems.PointerEventData");
                if (handlerType == null || pedType == null)
                    return new JObject { ["error"] = "uGUI EventSystem types not found (is com.unity.ugui present and a UI in the scene?)" };

                var ped = BuildPointerData(pedType);

                var handlers = new JArray();
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null || !handlerType.IsInstanceOfType(comp)) continue;
                    // Mimic a full click so handlers that track press state behave correctly.
                    Invoke(comp, "OnPointerEnter", ped);
                    Invoke(comp, "OnPointerDown", ped);
                    Invoke(comp, "OnPointerUp", ped);
                    Invoke(comp, "OnPointerClick", ped);
                    handlers.Add(comp.GetType().Name);
                }

                if (handlers.Count == 0)
                    return new JObject { ["error"] = $"No clickable UI handler (IPointerClickHandler) on {target}. Try runtime_ui_list to find clickable elements." };

                return new JObject
                {
                    ["clicked"] = true,
                    ["target"] = RtResolve.Path(go),
                    ["handlers"] = handlers,
                };
            }
            catch (Exception e) { return new JObject { ["error"] = e.Message }; }
        }

        /// <summary>Construct a PointerEventData bound to the active EventSystem (left button, default position).</summary>
        private static object BuildPointerData(Type pedType)
        {
            object es = null;
            var esType = RtResolve.ResolveType("UnityEngine.EventSystems.EventSystem");
            if (esType != null)
                es = esType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var ctor = pedType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 1);
            return ctor != null ? ctor.Invoke(new[] { es }) : null;
        }

        /// <summary>Invoke a handler method (e.g. OnPointerClick) on a component if it declares one taking PointerEventData.</summary>
        private static void Invoke(Component comp, string method, object ped)
        {
            if (ped == null) return;
            var m = comp.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, new[] { ped.GetType() }, null);
            if (m != null) { try { m.Invoke(comp, new[] { ped }); } catch { /* handler threw — ignore, it's game code */ } }
        }
    }
}
