using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Clears baked lightmap data and the GI disk cache via Lightmapping.Clear()
    /// and Lightmapping.ClearDiskCache(). Runs synchronously on the main thread.
    /// </summary>
    public class LightingClearTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "lighting_clear";

        /// <inheritdoc />
        public override string Description =>
            "Clear baked lightmap data and the GI disk cache via Lightmapping.Clear() / Lightmapping.ClearDiskCache().";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Clear lightmap data and the disk cache.
        /// </summary>
        /// <param name="parameters">No parameters.</param>
        /// <returns>{ cleared: true }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                Lightmapping.Clear();
                Lightmapping.ClearDiskCache();
                return new JObject { ["cleared"] = true };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
