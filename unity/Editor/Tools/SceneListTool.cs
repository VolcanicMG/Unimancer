using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Lists the scenes registered in Build Settings (path + enabled) and the
    /// scenes currently open in the Editor (path, isLoaded, isDirty).
    /// </summary>
    public class SceneListTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "scene_list";

        /// <inheritdoc />
        public override string Description =>
            "List Build Settings scenes and currently open scenes.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Collect Build Settings and open scenes.
        /// </summary>
        /// <param name="parameters">Unused.</param>
        /// <returns>{ buildSettings[], open[] }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var buildSettings = new JArray();
                foreach (var s in EditorBuildSettings.scenes)
                {
                    buildSettings.Add(new JObject
                    {
                        ["path"] = s.path,
                        ["enabled"] = s.enabled,
                    });
                }

                var open = new JArray();
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    Scene scene = EditorSceneManager.GetSceneAt(i);
                    open.Add(new JObject
                    {
                        ["path"] = scene.path,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty,
                    });
                }

                return new JObject
                {
                    ["buildSettings"] = buildSettings,
                    ["open"] = open,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
