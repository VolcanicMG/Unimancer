using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Unimancer
{
    /// <summary>
    /// Lists installed UPM packages via Client.List. Async because the request
    /// completes over several Editor frames; we poll IsCompleted on
    /// EditorApplication.update and then resolve the TaskCompletionSource.
    /// </summary>
    public class PackageListTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "package_list";

        /// <inheritdoc />
        public override string Description =>
            "List installed Package Manager packages. Returns [{ name, version, source }].";

        /// <inheritdoc />
        public override bool IsAsync => true;

        /// <summary>
        /// Kick off Client.List and resolve once the request finishes.
        /// </summary>
        /// <param name="parameters">Ignored (no parameters).</param>
        /// <param name="tcs">Completion source resolved with the package list.</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                ListRequest request = Client.List(true);
                EditorApplication.CallbackFunction poll = null;
                poll = () =>
                {
                    if (!request.IsCompleted) return;
                    EditorApplication.update -= poll;
                    try
                    {
                        if (request.Status != StatusCode.Success)
                        {
                            tcs.TrySetResult(new JObject { ["error"] = request.Error?.message ?? "package list failed" });
                            return;
                        }

                        var packages = new JArray();
                        foreach (var p in request.Result)
                        {
                            packages.Add(new JObject
                            {
                                ["name"] = p.name,
                                ["version"] = p.version,
                                ["source"] = p.source.ToString(),
                            });
                        }
                        tcs.TrySetResult(new JObject { ["packages"] = packages });
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
