using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Simulate a drag/swipe gesture on a running uGUI element (ScrollRect, draggable
    /// item, or any IDragHandler) to pan/scroll the view or test drag interactions.
    /// Dispatches pointer-down → begin-drag → drag(×steps) → end-drag → pointer-up via
    /// reflection on UnityEngine.EventSystems (no hard uGUI dependency). NOTE: this only
    /// reaches EventSystem-based drag handlers — games that poll Input/touches directly
    /// won't see it (use runtime_camera_control to move that kind of view).
    /// </summary>
    public class PointerDragTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_pointer_drag";

        /// <inheritdoc/>
        public override string Description =>
            "Drag/swipe on a running uGUI element (ScrollRect / IDragHandler) to pan or scroll. params: target (name/path), and either from [x,y] + to [x,y] (screen px) or delta [dx,dy]; optional steps (default 8). Dispatches begin/drag/end-drag. Returns { dragged, target, handlers, from, to }. Only reaches EventSystem UI — not Input-polling games (use runtime_camera_control there).";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = RtResolve.Resolve(target);
                if (go == null) return new JObject { ["error"] = $"GameObject not found: {target}" };
                if (!go.activeInHierarchy) return new JObject { ["error"] = $"GameObject is inactive (can't drag): {target}" };

                var dragType = RtResolve.ResolveType("UnityEngine.EventSystems.IDragHandler");
                var pedType = RtResolve.ResolveType("UnityEngine.EventSystems.PointerEventData");
                if (dragType == null || pedType == null)
                    return new JObject { ["error"] = "uGUI EventSystem types not found (is com.unity.ugui present?)" };

                Vector2 from = TryVec2(parameters, "from", out var f) ? f : new Vector2(Screen.width / 2f, Screen.height / 2f);
                Vector2 to;
                if (TryVec2(parameters, "to", out var tt)) to = tt;
                else if (TryVec2(parameters, "delta", out var dd)) to = from + dd;
                else to = from + new Vector2(0f, -200f); // default: a downward swipe
                int steps = Mathf.Clamp(parameters["steps"] != null ? (int)parameters["steps"] : 8, 1, 64);

                var ped = BuildPointerData(pedType);
                SetProp(ped, "position", from);       // button defaults to InputButton.Left (0)
                SetProp(ped, "pressPosition", from);

                var handlers = new JArray();
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null || !dragType.IsInstanceOfType(comp)) continue;
                    Invoke(comp, "OnPointerDown", ped);
                    Invoke(comp, "OnBeginDrag", ped);
                    Vector2 prev = from;
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector2 p = Vector2.Lerp(from, to, (float)i / steps);
                        SetProp(ped, "delta", p - prev);
                        SetProp(ped, "position", p);
                        Invoke(comp, "OnDrag", ped);
                        prev = p;
                    }
                    SetProp(ped, "delta", Vector2.zero);
                    SetProp(ped, "position", to);
                    Invoke(comp, "OnEndDrag", ped);
                    Invoke(comp, "OnPointerUp", ped);
                    handlers.Add(comp.GetType().Name);
                }

                if (handlers.Count == 0)
                    return new JObject { ["error"] = $"No drag handler (IDragHandler) on {target}. For map/camera panning that uses Input polling, use runtime_camera_control instead." };

                return new JObject
                {
                    ["dragged"] = true,
                    ["target"] = RtResolve.Path(go),
                    ["handlers"] = handlers,
                    ["from"] = new JArray { from.x, from.y },
                    ["to"] = new JArray { to.x, to.y },
                };
            }
            catch (Exception e) { return new JObject { ["error"] = e.Message }; }
        }

        /// <summary>Construct a PointerEventData bound to the active EventSystem (if any).</summary>
        private static object BuildPointerData(Type pedType)
        {
            object es = null;
            var esType = RtResolve.ResolveType("UnityEngine.EventSystems.EventSystem");
            if (esType != null)
                es = esType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var ctor = pedType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 1);
            return ctor != null ? ctor.Invoke(new[] { es }) : null;
        }

        /// <summary>Set a writable PointerEventData property (position/delta/pressPosition) via reflection.</summary>
        private static void SetProp(object ped, string name, object value)
        {
            if (ped == null) return;
            var p = ped.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { try { p.SetValue(ped, value); } catch { /* ignore */ } }
        }

        /// <summary>Invoke a drag/pointer handler method taking PointerEventData, if present.</summary>
        private static void Invoke(Component comp, string method, object ped)
        {
            if (ped == null) return;
            var m = comp.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, new[] { ped.GetType() }, null);
            if (m != null) { try { m.Invoke(comp, new[] { ped }); } catch { /* game code threw — ignore */ } }
        }

        /// <summary>Parse a [x,y] JSON array parameter into a Vector2.</summary>
        private static bool TryVec2(JObject p, string key, out Vector2 v)
        {
            v = default;
            if (!(p[key] is JArray a) || a.Count < 2) return false;
            v = new Vector2((float)a[0], (float)a[1]);
            return true;
        }
    }
}
