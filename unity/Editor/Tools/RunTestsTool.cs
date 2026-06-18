using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;

namespace Unimancer
{
    /// <summary>
    /// Runs EditMode or PlayMode tests via the Unity Test Runner API
    /// (UnityEditor.TestTools.TestRunner.Api.TestRunnerApi). Async: registers a
    /// callback collecting results, starts the run, and resolves the
    /// TaskCompletionSource on RunFinished.
    ///
    /// NOTE: Requires the package com.unity.test-framework to be present in the
    /// project; the UnityEditor.TestTools.TestRunner.Api namespace ships with it.
    /// </summary>
    public class RunTestsTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "run_tests";

        /// <inheritdoc />
        public override string Description =>
            "Run EditMode or PlayMode tests (optional name/category filter). Returns { passed, failed, skipped, durationSeconds, failures }.";

        /// <inheritdoc />
        public override bool IsAsync => true;

        /// <summary>
        /// Build a Filter, register a result callback, and start the run.
        /// </summary>
        /// <param name="parameters">mode ('EditMode' | 'PlayMode'), filter (optional).</param>
        /// <param name="tcs">Completion source resolved with the run summary.</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                var modeStr = parameters["mode"]?.ToString();
                if (modeStr != "EditMode" && modeStr != "PlayMode")
                {
                    tcs.TrySetResult(new JObject { ["error"] = $"mode must be EditMode or PlayMode (got '{modeStr}')" });
                    return;
                }

                var testMode = modeStr == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
                var filterStr = parameters["filter"]?.ToString();

                var filter = new Filter { testMode = testMode };
                if (!string.IsNullOrEmpty(filterStr))
                {
                    // A filter string targets either an explicit test name or a
                    // category; pass it as both so either match works.
                    filter.testNames = new[] { filterStr };
                    filter.categoryNames = new[] { filterStr };
                }

                var api = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
                var collector = new ResultCollector(tcs, api);
                api.RegisterCallbacks(collector);
                api.Execute(new ExecutionSettings(filter));
            }
            catch (Exception e)
            {
                tcs.TrySetResult(new JObject { ["error"] = e.Message });
            }
        }

        /// <summary>
        /// ICallbacks implementation that tallies leaf-test results and resolves
        /// the completion source when the whole run finishes.
        /// </summary>
        private class ResultCollector : ICallbacks
        {
            private readonly TaskCompletionSource<JObject> _tcs;
            private readonly TestRunnerApi _api;
            private int _passed;
            private int _failed;
            private int _skipped;
            private readonly JArray _failures = new JArray();

            /// <summary>
            /// Create a collector bound to a completion source and the running API.
            /// </summary>
            /// <param name="tcs">Completion source to resolve on RunFinished.</param>
            /// <param name="api">The API instance (unregistered on finish).</param>
            public ResultCollector(TaskCompletionSource<JObject> tcs, TestRunnerApi api)
            {
                _tcs = tcs;
                _api = api;
            }

            /// <summary>Called when the whole run starts (no-op).</summary>
            /// <param name="testsToRun">The root of the tests to run.</param>
            public void RunStarted(ITestAdaptor testsToRun) { }

            /// <summary>Called when a single test starts (no-op).</summary>
            /// <param name="test">The test about to run.</param>
            public void TestStarted(ITestAdaptor test) { }

            /// <summary>
            /// Tally each leaf test result as it finishes.
            /// </summary>
            /// <param name="result">The finished test (or suite) result.</param>
            public void TestFinished(ITestResultAdaptor result)
            {
                // Only count leaf tests, not suites/fixtures.
                if (result.Test.IsSuite) return;

                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        _passed++;
                        break;
                    case TestStatus.Skipped:
                        _skipped++;
                        break;
                    case TestStatus.Failed:
                    case TestStatus.Inconclusive:
                        _failed++;
                        _failures.Add(new JObject
                        {
                            ["name"] = result.Test.FullName,
                            ["message"] = result.Message ?? "",
                        });
                        break;
                }
            }

            /// <summary>
            /// Resolve the completion source with the run summary.
            /// </summary>
            /// <param name="result">The root result for the whole run.</param>
            public void RunFinished(ITestResultAdaptor result)
            {
                try { _api.UnregisterCallbacks(this); } catch { /* best effort */ }

                _tcs.TrySetResult(new JObject
                {
                    ["passed"] = _passed,
                    ["failed"] = _failed,
                    ["skipped"] = _skipped,
                    ["durationSeconds"] = result.Duration,
                    ["failures"] = _failures,
                });
            }
        }
    }
}
