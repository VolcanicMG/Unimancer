using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unimancer.Runtime
{
    /// <summary>
    /// Finds GameObjects in the RUNNING game by name (substring or exact). Searches
    /// all objects, optionally including inactive ones, dedups by instance id, and
    /// caps results. instanceID is returned as a STRING because EntityId values can
    /// exceed the safe JS integer range.
    /// </summary>
    public class FindObjectsTool : RuntimeToolBase
    {
        private const int MaxResults = 200;

        /// <inheritdoc/>
        public override string Name => "runtime_find_objects";

        /// <inheritdoc/>
        public override string Description =>
            "Find GameObjects in the running game by name. params: query (string), exact? (bool), includeInactive? (bool, default true). Returns up to 200 {name, path, instanceID, active}.";

        /// <inheritdoc/>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var query = parameters["query"]?.ToString() ?? "";
                var exact = parameters["exact"]?.ToObject<bool>() ?? false;
                var includeInactive = parameters["includeInactive"]?.ToObject<bool>() ?? true;

                // Two-arg overload (no FindObjectsSortMode) — the SortMode variants are
                // deprecated in Unity 6 (CS0618); result order is not relied upon.
                var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                    includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);

                var seen = new HashSet<string>();
                var results = new JArray();
                foreach (var go in all)
                {
                    if (results.Count >= MaxResults) break;

                    bool match = exact ? go.name == query : go.name.Contains(query);
                    if (!match) continue;

                    var id = EntityId.ToULong(go.GetEntityId()).ToString();
                    if (!seen.Add(id)) continue; // dedup by instance id.

                    results.Add(new JObject
                    {
                        ["name"] = go.name,
                        ["path"] = RtResolve.Path(go),
                        ["instanceID"] = id,
                        ["active"] = go.activeInHierarchy,
                    });
                }

                return new JObject { ["results"] = results, ["count"] = results.Count };
            }
            catch (System.Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
