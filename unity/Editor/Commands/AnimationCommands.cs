using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Unimancer.Commands
{
    /// <summary>
    /// Animation commands contributed to com.unity.pipeline.
    /// </summary>
    public static class AnimationCommands
    {
        /// <summary>
        /// Set an <see cref="Animator"/> parameter by name. The parameter type is
        /// inferred from the string value (omitted -&gt; trigger, "true"/"false" -&gt;
        /// bool, integral -&gt; int, otherwise float) unless <c>type</c> forces it.
        /// </summary>
        [CliCommand("animator_set_parameter",
            "Set an Animator parameter; type inferred from value (bool/int/float/trigger) unless `type` forces it. " +
            "Most meaningful in Play mode, where the Animator is actively evaluating; in edit mode the value generally does not persist.",
            Tags = new[] { "animation" })]
        public static Dictionary<string, object> AnimatorSetParameter(
            [CliArg("target", "Hierarchy path or instanceID of the GameObject carrying the Animator.", Required = true)] string target,
            [CliArg("name", "Animator parameter name.", Required = true)] string name,
            [CliArg("value", "Parameter value; omit to fire a Trigger.")] string value = null,
            [CliArg("type", "Force the parameter type: float | int | bool | trigger. Omit to infer from value.")] string type = null)
        {
            var go = GoResolve.Resolve(target);
            if (go == null)
                throw new ArgumentException($"GameObject not found: {target}");

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                throw new ArgumentException($"no Animator on GameObject: {target}");

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required");

            var resolved = string.IsNullOrEmpty(type) ? Infer(value) : type.ToLowerInvariant();
            switch (resolved)
            {
                case "trigger":
                    animator.SetTrigger(name);
                    break;
                case "bool":
                    animator.SetBool(name, ParseBool(value));
                    break;
                case "int":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        throw new ArgumentException($"value is not an int: {value}");
                    animator.SetInteger(name, i);
                    break;
                case "float":
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        throw new ArgumentException($"value is not a float: {value}");
                    animator.SetFloat(name, f);
                    break;
                default:
                    throw new ArgumentException($"unknown type: {type} (expected float, int, bool or trigger)");
            }

            return new Dictionary<string, object>
            {
                ["set"] = true,
                ["name"] = name,
                ["type"] = resolved,
            };
        }

        /// <summary>Infer the Animator parameter type from the raw string value.</summary>
        /// <param name="value">The supplied value, or null/empty for a trigger.</param>
        /// <returns>"trigger", "bool", "int" or "float".</returns>
        /// <exception cref="ArgumentException">The value is not a bool or a number.</exception>
        private static string Infer(string value)
        {
            if (string.IsNullOrEmpty(value)) return "trigger";
            if (string.Equals(value, "trigger", StringComparison.OrdinalIgnoreCase)) return "trigger";
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return "bool";
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return "int";
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return "float";
            throw new ArgumentException($"unsupported value: {value} (expected true/false, a number, or omit for a trigger)");
        }

        /// <summary>Parse a bool value, accepting only true/false (case-insensitive).</summary>
        private static bool ParseBool(string value)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
            throw new ArgumentException($"value is not a bool: {value}");
        }
    }
}
