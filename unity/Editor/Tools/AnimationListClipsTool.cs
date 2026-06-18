using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// List AnimationClip asset paths under a folder using
    /// AssetDatabase.FindAssets("t:AnimationClip", ...). Results are capped to
    /// keep the response bounded.
    /// </summary>
    public class AnimationListClipsTool : McpToolBase
    {
        /// <summary>Maximum number of clip paths returned.</summary>
        private const int MaxClips = 200;

        /// <inheritdoc />
        public override string Name => "animation_list_clips";

        /// <inheritdoc />
        public override string Description =>
            "List AnimationClip asset paths under a folder (default Assets); capped at 200.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Search the folder for AnimationClip assets.</summary>
        /// <param name="parameters">folder (optional, defaults to Assets).</param>
        /// <returns>{ clips:[...], count } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var folder = parameters["folder"]?.ToString();
                if (string.IsNullOrEmpty(folder))
                    folder = "Assets";

                var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });

                var clips = new JArray();
                var seen = new HashSet<string>();
                foreach (var guid in guids)
                {
                    if (clips.Count >= MaxClips)
                        break;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !seen.Add(path))
                        continue;
                    clips.Add(path);
                }

                return new JObject
                {
                    ["clips"] = clips,
                    ["count"] = clips.Count,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
