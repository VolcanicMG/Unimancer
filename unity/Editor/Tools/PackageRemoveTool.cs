using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Unimancer
{
    /// <summary>
    /// Removes a UPM package via Client.Remove. Async because the request
    /// completes over several Editor frames; we poll IsCompleted on
    /// EditorApplication.update and then resolve.
    /// </summary>
    public class PackageRemoveTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "package_remove";

        /// <inheritdoc />
        public override string Description =>
            "Remove a package by name. Returns { removed } or { error }.";

        /// <inheritdoc />
        public override bool IsAsync => true;

        /// <summary>
        /// Kick off Client.Remove and resolve once the request finishes.
        /// </summary>
        /// <param name="parameters">name (required).</param>
        /// <param name="tcs">Completion source resolved with { removed }.</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                var name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                {
                    tcs.TrySetResult(new JObject { ["error"] = "name is required" });
                    return;
                }

                RemoveRequest request = Client.Remove(name);
                EditorApplication.CallbackFunction poll = null;
                poll = () =>
                {
                    if (!request.IsCompleted) return;
                    EditorApplication.update -= poll;
                    try
                    {
                        if (request.Status != StatusCode.Success)
                        {
                            tcs.TrySetResult(new JObject { ["error"] = request.Error?.message ?? "package remove failed" });
                            return;
                        }
                        tcs.TrySetResult(new JObject { ["removed"] = true, ["name"] = request.PackageIdOrName });
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetResult(new JObject { ["error"] = e.Message });
                    }
                };
                EditorApplication.update += poll;
            }
            catch (Exception e)
            {
                tcs.TrySetResult(new JObject { ["error"] = e.Message });
            }
        }
    }
}
