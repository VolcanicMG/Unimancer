using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Switches the active Editor build target to Android. Synchronous Unity API
    /// work, marshalled onto the main thread by the bridge.
    /// </summary>
    public class AndroidSwitchPlatformTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "android_switch_platform";

        /// <inheritdoc />
        public override string Description =>
            "Switch the active Unity build target to Android.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Switch to the Android build target and report the resulting active target.
        /// </summary>
        /// <param name="parameters">Unused (no parameters).</param>
        /// <returns>{ switched, active } or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
                return new JObject
                {
                    ["switched"] = true,
                    ["active"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
