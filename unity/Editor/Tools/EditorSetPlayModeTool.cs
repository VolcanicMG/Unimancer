using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Enters, pauses, or exits play mode by toggling
    /// EditorApplication.isPlaying / isPaused, then returns the new state.
    /// Runs synchronously on the main thread.
    /// </summary>
    public class EditorSetPlayModeTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "editor_set_play_mode";

        /// <inheritdoc />
        public override string Description =>
            "Set the Editor play mode (play | pause | stop) and return the new state.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Apply the requested play mode.
        /// </summary>
        /// <param name="parameters">mode: 'play' | 'pause' | 'stop'.</param>
        /// <returns>New play state, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var mode = parameters["mode"]?.ToString();
                switch (mode)
                {
                    case "play":
                        EditorApplication.isPlaying = true;
                        EditorApplication.isPaused = false;
                        break;
                    case "pause":
                        // Pause only makes sense while playing; set both so the
                        // Editor enters play mode paused if not already running.
                        EditorApplication.isPlaying = true;
                        EditorApplication.isPaused = true;
                        break;
                    case "stop":
                        EditorApplication.isPlaying = false;
                        break;
                    default:
                        return new JObject { ["error"] = $"unknown mode: {mode}" };
                }

                return new JObject
                {
                    ["isPlaying"] = EditorApplication.isPlaying,
                    ["isPaused"] = EditorApplication.isPaused,
                    ["activeScene"] = EditorSceneManager.GetActiveScene().path,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
