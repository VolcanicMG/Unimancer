using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Introspect the <see cref="Animator"/> on a GameObject: the controller asset
    /// path, its parameters, and per-layer state names. State and parameter detail
    /// require the runtimeAnimatorController to be an editable
    /// <see cref="AnimatorController"/> asset (UnityEditor.Animations); a plain
    /// RuntimeAnimatorController or override controller may not expose state graph
    /// detail.
    /// </summary>
    public class AnimatorGetTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "animator_get";

        /// <inheritdoc />
        public override string Description =>
            "Read a GameObject's Animator: controller path, parameters, and state names. Returns { hasAnimator } false if none.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the Animator and read its controller graph.</summary>
        /// <param name="parameters">target (required).</param>
        /// <returns>{ hasAnimator, controller, parameters, states } or { hasAnimator:false } / { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var target = parameters["target"]?.ToString();
                var go = GoResolve.Resolve(target);
                if (go == null)
                    return new JObject { ["error"] = $"GameObject not found: {target}" };

                var animator = go.GetComponent<Animator>();
                if (animator == null)
                    return new JObject { ["hasAnimator"] = false };

                var runtime = animator.runtimeAnimatorController;
                var controllerPath = runtime != null ? AssetDatabase.GetAssetPath(runtime) : null;

                var paramsArr = new JArray();
                var statesArr = new JArray();

                // The editable AnimatorController exposes parameters and the state graph.
                // An override controller stores its base under runtimeAnimatorController.
                var controller = runtime as AnimatorController;
                if (controller == null && runtime is AnimatorOverrideController over)
                    controller = over.runtimeAnimatorController as AnimatorController;

                if (controller != null)
                {
                    foreach (var p in controller.parameters)
                    {
                        var entry = new JObject
                        {
                            ["name"] = p.name,
                            ["type"] = p.type.ToString(),
                        };
                        switch (p.type)
                        {
                            case AnimatorControllerParameterType.Bool:
                                entry["defaultValue"] = p.defaultBool;
                                break;
                            case AnimatorControllerParameterType.Int:
                                entry["defaultValue"] = p.defaultInt;
                                break;
                            case AnimatorControllerParameterType.Float:
                                entry["defaultValue"] = p.defaultFloat;
                                break;
                            case AnimatorControllerParameterType.Trigger:
                                entry["defaultValue"] = p.defaultBool;
                                break;
                        }
                        paramsArr.Add(entry);
                    }

                    foreach (var layer in controller.layers)
                    {
                        var sm = layer.stateMachine;
                        if (sm == null)
                            continue;
                        foreach (var cs in sm.states)
                        {
                            if (cs.state == null)
                                continue;
                            statesArr.Add($"{layer.name}.{cs.state.name}");
                        }
                    }
                }

                return new JObject
                {
                    ["hasAnimator"] = true,
                    ["controller"] = controllerPath,
                    ["parameters"] = paramsArr,
                    ["states"] = statesArr,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
