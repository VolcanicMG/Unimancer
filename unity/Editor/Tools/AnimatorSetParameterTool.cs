using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Set an <see cref="Animator"/> parameter by name, inferring the parameter
    /// type from the supplied value: boolean -&gt; SetBool, integral number -&gt;
    /// SetInteger, other number -&gt; SetFloat, null or "trigger" -&gt; SetTrigger.
    /// NOTE: this is most meaningful in Play mode, where the Animator is actively
    /// evaluating; in edit mode the set value generally does not persist.
    /// </summary>
    public class AnimatorSetParameterTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "animator_set_parameter";

        /// <inheritdoc />
        public override string Description =>
            "Set an Animator parameter; type inferred from value (bool/int/float/trigger). Most meaningful in Play mode.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Resolve the Animator and set the parameter by inferred type.</summary>
        /// <param name="parameters">target (required), name (required), value (optional).</param>
        /// <returns>{ set:true, name, type } or { error }.</returns>
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
                    return new JObject { ["error"] = $"no Animator on GameObject: {target}" };

                var name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                    return new JObject { ["error"] = "name is required" };

                var valueToken = parameters["value"];
                string type;

                // Infer the parameter type from the JSON value kind.
                if (valueToken == null || valueToken.Type == JTokenType.Null)
                {
                    animator.SetTrigger(name);
                    type = "trigger";
                }
                else if (valueToken.Type == JTokenType.Boolean)
                {
                    animator.SetBool(name, (bool)valueToken);
                    type = "bool";
                }
                else if (valueToken.Type == JTokenType.String)
                {
                    var s = valueToken.ToString();
                    // A literal "trigger" string fires a trigger; other strings are unsupported.
                    if (string.Equals(s, "trigger", StringComparison.OrdinalIgnoreCase))
                    {
                        animator.SetTrigger(name);
                        type = "trigger";
                    }
                    else
                    {
                        return new JObject { ["error"] = $"unsupported string value: {s}" };
                    }
                }
                else if (valueToken.Type == JTokenType.Integer)
                {
                    animator.SetInteger(name, (int)valueToken);
                    type = "int";
                }
                else
                {
                    // Float, or any other numeric token.
                    animator.SetFloat(name, (float)valueToken);
                    type = "float";
                }

                return new JObject
                {
                    ["set"] = true,
                    ["name"] = name,
                    ["type"] = type,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
