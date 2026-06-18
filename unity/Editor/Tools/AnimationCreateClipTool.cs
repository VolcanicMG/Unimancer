using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Unimancer
{
    /// <summary>
    /// Create a new <see cref="AnimationClip"/> asset on disk with a requested
    /// frame rate and optional legacy flag. The target path is PathGuard-validated
    /// so writes stay inside the project's Assets/ tree.
    /// </summary>
    public class AnimationCreateClipTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "animation_create_clip";

        /// <inheritdoc />
        public override string Description =>
            "Create a new AnimationClip asset at path (under Assets/, .anim). frameRate defaults to 60; legacy optional.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Construct the clip and write it via AssetDatabase.</summary>
        /// <param name="parameters">path (required), frameRate (optional), legacy (optional).</param>
        /// <returns>{ created:&lt;path&gt; } or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();

                // Keep the write inside Assets/ and reject path traversal.
                var guard = PathGuard.Validate(path);
                if (guard != null)
                    return guard;

                var frameRate = parameters["frameRate"] != null ? (float)parameters["frameRate"] : 60f;
                var legacy = parameters["legacy"] != null && (bool)parameters["legacy"];

                var clip = new AnimationClip { frameRate = frameRate };
                if (legacy)
                    clip.legacy = true;

                AssetDatabase.CreateAsset(clip, path);
                AssetDatabase.SaveAssets();

                return new JObject { ["created"] = path };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
