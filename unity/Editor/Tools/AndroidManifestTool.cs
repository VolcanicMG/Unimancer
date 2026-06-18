using System;
using System.IO;
using System.Linq;
using System.Xml;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Lists, adds, and removes &lt;uses-permission&gt; entries in the project's
    /// Android manifest at Assets/Plugins/Android/AndroidManifest.xml. On 'add'
    /// with no manifest present, a minimal one is created. Uses System.Xml.
    /// </summary>
    public class AndroidManifestTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "android_manifest";

        /// <inheritdoc />
        public override string Description =>
            "List/add/remove Android manifest permissions in Assets/Plugins/Android/AndroidManifest.xml.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        // Project-relative manifest location Unity merges into the build.
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        /// <summary>
        /// Apply the requested action to the manifest's permission list.
        /// </summary>
        /// <param name="parameters">action ('list'|'add'|'remove') and optional permissions array.</param>
        /// <returns>{ permissions } resulting list, or { error } on failure.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                var action = parameters["action"]?.ToString() ?? "list";
                var permissions = (parameters["permissions"] as JArray)?.Select(p => p.ToString()).ToArray()
                                  ?? Array.Empty<string>();

                if (action == "list" && !File.Exists(ManifestPath))
                    return new JObject { ["permissions"] = new JArray(), ["manifestExists"] = false };

                if (action == "add" && !File.Exists(ManifestPath))
                    CreateMinimalManifest();
                else if (action != "add" && !File.Exists(ManifestPath))
                    return new JObject { ["permissions"] = new JArray(), ["manifestExists"] = false };

                var doc = new XmlDocument();
                doc.Load(ManifestPath);
                var manifestEl = doc.DocumentElement; // <manifest>

                if (action == "add")
                    AddPermissions(doc, manifestEl, permissions);
                else if (action == "remove")
                    RemovePermissions(doc, manifestEl, permissions);

                if (action != "list")
                {
                    doc.Save(ManifestPath);
                    AssetDatabase.Refresh();
                }

                return new JObject
                {
                    ["manifestExists"] = true,
                    ["permissions"] = new JArray(ListPermissions(doc).Cast<object>().ToArray()),
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        /// <summary>Write a minimal manifest with an empty &lt;application&gt; node.</summary>
        private static void CreateMinimalManifest()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
            const string minimal =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n" +
                "  <application />\n" +
                "</manifest>\n";
            File.WriteAllText(ManifestPath, minimal);
        }

        /// <summary>Add &lt;uses-permission&gt; nodes that are not already present.</summary>
        /// <param name="doc">The manifest document.</param>
        /// <param name="manifestEl">The root &lt;manifest&gt; element.</param>
        /// <param name="permissions">Permission names to add.</param>
        private static void AddPermissions(XmlDocument doc, XmlElement manifestEl, string[] permissions)
        {
            var existing = ListPermissions(doc);
            foreach (var perm in permissions)
            {
                if (existing.Contains(perm)) continue;
                var node = doc.CreateElement("uses-permission");
                var attr = doc.CreateAttribute("android", "name", AndroidNs);
                attr.Value = perm;
                node.Attributes.Append(attr);
                manifestEl.AppendChild(node);
            }
        }

        /// <summary>Remove any &lt;uses-permission&gt; node matching the given names.</summary>
        /// <param name="doc">The manifest document.</param>
        /// <param name="manifestEl">The root &lt;manifest&gt; element.</param>
        /// <param name="permissions">Permission names to remove.</param>
        private static void RemovePermissions(XmlDocument doc, XmlElement manifestEl, string[] permissions)
        {
            var nodes = manifestEl.SelectNodes("uses-permission");
            if (nodes == null) return;
            foreach (XmlNode node in nodes.Cast<XmlNode>().ToList())
            {
                var name = (node as XmlElement)?.GetAttribute("name", AndroidNs);
                if (name != null && permissions.Contains(name))
                    manifestEl.RemoveChild(node);
            }
        }

        /// <summary>Collect the android:name of every &lt;uses-permission&gt; node.</summary>
        /// <param name="doc">The manifest document.</param>
        /// <returns>The current permission names.</returns>
        private static System.Collections.Generic.List<string> ListPermissions(XmlDocument doc)
        {
            var result = new System.Collections.Generic.List<string>();
            var nodes = doc.DocumentElement?.SelectNodes("uses-permission");
            if (nodes == null) return result;
            foreach (XmlNode node in nodes)
            {
                var name = (node as XmlElement)?.GetAttribute("name", AndroidNs);
                if (!string.IsNullOrEmpty(name)) result.Add(name);
            }
            return result;
        }
    }
}
