using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Reports high-level state of the RUNNING game: active/loaded scenes, root
    /// object count, time scale, frame count, pause state, target frame rate, and
    /// platform. Read-only orientation tool.
    /// </summary>
    public class SceneInfoTool : RuntimeToolBase
    {
        /// <inheritdoc/>
        public override string Name => "runtime_scene_info";

        /// <inheritdoc/>
        public override string Description =>
            "Snapshot of the RUNNING game: active scene, all loaded scenes, root object count, timeScale, time, frameCount, pause state, target frame rate, and platform.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var active = SceneManager.GetActiveScene();

                var loaded = new JArray();
                int rootCount = 0;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    loaded.Add(s.name);
                    if (s.isLoaded) rootCount += s.rootCount;
                }

                return new JObject
                {
                    ["activeScene"] = active.name,
                    ["loadedScenes"] = loaded,
                    ["rootCount"] = rootCount,
                    ["timeScale"] = Time.timeScale,
                    ["time"] = Time.time,
                    ["frameCount"] = Time.frameCount,
                    ["isPaused"] = Time.timeScale == 0f,
                    ["targetFrameRate"] = Application.targetFrameRate,
                    ["platform"] = Application.platform.ToString(),
                };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
