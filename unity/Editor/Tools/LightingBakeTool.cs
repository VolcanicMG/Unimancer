using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Bakes the scene lightmaps via Lightmapping.BakeAsync(). A lightmap bake
    /// spans many Editor frames, so this is an async tool: it starts the bake on
    /// the main thread, then polls Lightmapping.isRunning each EditorApplication
    /// update tick and resolves once the bake completes.
    /// </summary>
    public class LightingBakeTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "lighting_bake";

        /// <inheritdoc />
        public override string Description =>
            "Bake the scene lightmaps via Lightmapping.BakeAsync(), waiting for the multi-frame bake to complete.";

        /// <inheritdoc />
        public override bool IsAsync => true;

        // Number of update ticks to wait for the bake to actually start before
        // giving up. isRunning can lag a frame or two after BakeAsync() returns
        // true, so we tolerate a short startup window rather than resolving early.
        private const int StartupGraceTicks = 120;

        /// <summary>
        /// Start the async lightmap bake and poll for completion. Called on the
        /// Unity main thread by the bridge.
        /// </summary>
        /// <param name="parameters">No parameters.</param>
        /// <param name="tcs">Completion source resolved with the bake result.</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                bool started = Lightmapping.BakeAsync();
                if (!started)
                {
                    // Couldn't start (e.g. another bake in progress, or auto-bake on).
                    tcs.TrySetResult(new JObject
                    {
                        ["baked"] = false,
                        ["error"] = "Lightmapping.BakeAsync() returned false; the bake could not be started.",
                    });
                    return;
                }

                bool resolved = false;          // guard against double-resolve
                bool sawRunning = false;        // confirm the bake actually began
                int ticks = 0;
                EditorApplication.CallbackFunction poller = null;

                poller = () =>
                {
                    try
                    {
                        ticks++;

                        if (Lightmapping.isRunning)
                        {
                            sawRunning = true;
                            return; // still baking
                        }

                        // Not running. Either it finished (sawRunning) or it never
                        // started within the grace window (guard against a bake
                        // that silently never begins).
                        if (sawRunning || ticks >= StartupGraceTicks)
                        {
                            if (!resolved)
                            {
                                resolved = true;
                                EditorApplication.update -= poller;

                                if (sawRunning)
                                    tcs.TrySetResult(new JObject { ["baked"] = true });
                                else
                                    tcs.TrySetResult(new JObject
                                    {
                                        ["baked"] = false,
                                        ["error"] = "Lightmapping bake never started (isRunning stayed false).",
                                    });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (!resolved)
                        {
                            resolved = true;
                            EditorApplication.update -= poller;
                            tcs.TrySetException(e);
                        }
                    }
                };

                EditorApplication.update += poller;
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }
    }
}
