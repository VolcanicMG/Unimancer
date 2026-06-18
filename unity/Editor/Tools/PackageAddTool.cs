using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Unimancer
{
    /// <summary>
    /// Adds a UPM package via Client.Add (name, name@version, or git URL). Async
    /// because the request completes over several Editor frames; we poll
    /// IsCompleted on EditorApplication.update and then resolve.
    /// </summary>
    public class PackageAddTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "package_add";

        /// <inheritdoc />
        public override string Description =>
            "Add a package by name, name@version, or git URL. Returns the added package or { error }.";

        /// <inheritdoc />
        public override bool IsAsync => true;

        /// <summary>
        /// Kick off Client.Add and resolve once the request finishes.
        /// </summary>
        /// <param name="parameters">identifier (required).</param>
        /// <param name="tcs">Completion source resolved with the added package.</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                var identifier = parameters["identifier"]?.ToString();
                if (string.IsNullOrEmpty(identifier))
                {
                    tcs.TrySetResult(new JObject { ["error"] = "identifier is required" });
                    return;
                }

                AddRequest request = Client.Add(identifier);
                EditorApplication.CallbackFunction poll = null;
                poll = () =>
                {
                    if (!request.IsCompleted) return;
                    EditorApplication.update -= poll;
                    try
                    {
                        if (request.Status != StatusCode.Success)
                        {
                            tcs.TrySetResult(new JObject { ["error"] = request.Error?.message ?? "package add failed" });
                            return;
                        }

                        var p = request.Result;
                        tcs.TrySetResult(new JObject
                        {
                            ["name"] = p.name,
                            ["version"] = p.version,
                            ["source"] = p.source.ToString(),
                        });
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
