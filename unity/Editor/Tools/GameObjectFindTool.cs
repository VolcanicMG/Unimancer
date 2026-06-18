using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unimancer
{
    /// <summary>
    /// Search the active scene's GameObjects by name, with optional tag and
    /// active-state filtering.
    /// </summary>
    public class GameObjectFindTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "gameobject_find";

        /// <inheritdoc />
        public override string Description =>
            "Find GameObjects by name; returns [{name, path, instanceID, active}].";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>Walk loaded scene GameObjects and collect matches.</summary>
        /// <param name="parameters">query (required), exact, tag, includeInactive.</param>
        /// <returns>{results:[...]} or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var query = parameters["query"]?.ToString();
                if (query == null)
                    return new JObject { ["error"] = "query is required" };

                var exact = parameters["exact"]?.ToObject<bool>() ?? false;
                var tag = parameters["tag"]?.ToString();
                var includeInactive = parameters["includeInactive"]?.ToObject<bool>() ?? false;

                var results = new JArray();

                // Resources.FindObjectsOfTypeAll surfaces inactive objects too.
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    // Restrict to actual scene objects (skip prefab assets/hidden).
                    if (!go.scene.IsValid())
                        continue;
                    if (!includeInactive && !go.activeInHierarchy)
                        continue;

                    var nameMatch = exact
                        ? go.name == query
                        : go.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!nameMatch)
                        continue;

                    if (!string.IsNullOrEmpty(tag) && !go.CompareTag(tag))
                        continue;

                    results.Add(new JObject
                    {
                        ["name"] = go.name,
                        ["path"] = GoResolve.Path(go),
                        ["instanceID"] = EntityId.ToULong(go.GetEntityId()),
                        ["active"] = go.activeInHierarchy,
                    });
                }

                return new JObject { ["results"] = results, ["count"] = results.Count };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
