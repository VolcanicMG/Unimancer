using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Moves or renames an asset, validating the move first via
    /// AssetDatabase.ValidateMoveAsset.
    /// </summary>
    public class AssetMoveTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "asset_move";

        /// <inheritdoc />
        public override string Description =>
            "Move or rename an asset (validated). Returns the error string (empty on success).";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Validate and perform the move.
        /// </summary>
        /// <param name="parameters">from (required), to (required).</param>
        /// <returns>{ moved, error } where error is empty on success.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var from = parameters["from"]?.ToString();
                var to = parameters["to"]?.ToString();
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                    return new JObject { ["error"] = "from and to are required" };

                var fromGuard = PathGuard.Validate(from);
                if (fromGuard != null) return fromGuard;
                var toGuard = PathGuard.Validate(to);
                if (toGuard != null) return toGuard;

                // ValidateMoveAsset returns "" when the move is allowed.
                var validation = AssetDatabase.ValidateMoveAsset(from, to);
                if (!string.IsNullOrEmpty(validation))
                    return new JObject { ["moved"] = false, ["error"] = validation };

                // MoveAsset also returns "" on success.
                var moveError = AssetDatabase.MoveAsset(from, to);
                return new JObject
                {
                    ["moved"] = string.IsNullOrEmpty(moveError),
                    ["error"] = moveError,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
