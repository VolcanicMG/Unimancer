using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Executes an Editor menu item by its menu path via
    /// EditorApplication.ExecuteMenuItem. Runs synchronously on the main thread.
    /// </summary>
    public class MenuExecuteTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "menu_execute";

        /// <inheritdoc />
        public override string Description =>
            "Execute an Editor menu item by menu path (e.g. 'Assets/Refresh'). Returns { executed }.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Invoke the menu item.
        /// </summary>
        /// <param name="parameters">menuPath (required).</param>
        /// <returns>{ executed }, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var menuPath = parameters["menuPath"]?.ToString();
                if (string.IsNullOrEmpty(menuPath))
                    return new JObject { ["error"] = "menuPath is required" };

                bool executed = EditorApplication.ExecuteMenuItem(menuPath);
                return new JObject { ["executed"] = executed };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
