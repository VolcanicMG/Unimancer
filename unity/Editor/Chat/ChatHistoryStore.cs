using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Unimancer
{
    /// <summary>One transcript line, persisted with a chat. Role is stored as an int (the Role enum).</summary>
    [Serializable]
    public class ChatLineDto
    {
        public int role;
        public string text;
    }

    /// <summary>A saved chat conversation: transcript + the Claude Code session id to --resume it.</summary>
    [Serializable]
    public class ChatConversation
    {
        public string id;          // our stable file id (GUID)
        public string title;       // derived from the first user message
        public string sessionId;   // Claude Code session id for --resume (may be empty)
        public long updatedTicks;  // DateTime.UtcNow.Ticks of last write, for sorting
        public List<ChatLineDto> lines = new List<ChatLineDto>();
    }

    /// <summary>
    /// File-backed store for Unimancer chat history so conversations survive window
    /// close / New chat. Lives under <c>&lt;Project&gt;/Library/Unimancer/Chats</c>,
    /// which Unity excludes from version control (git AND Unity Version Control), so
    /// recalled chats never pollute the repo.
    /// </summary>
    public static class ChatHistoryStore
    {
        /// <summary>Absolute path to the chat-history directory (created on demand).</summary>
        private static string Dir()
        {
            // Application.dataPath = <Project>/Assets → its parent is the project root.
            var root = Path.GetDirectoryName(Application.dataPath);
            var dir = Path.Combine(root, "Library", "Unimancer", "Chats");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string PathFor(string id) => Path.Combine(Dir(), id + ".json");

        /// <summary>Write (or overwrite) a conversation, stamping its updated time.</summary>
        public static void Save(ChatConversation c)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) return;
            c.updatedTicks = DateTime.UtcNow.Ticks;
            try { File.WriteAllText(PathFor(c.id), JsonUtility.ToJson(c)); }
            catch (Exception e) { Debug.LogWarning("[Unimancer] Chat save failed: " + e.Message); }
        }

        /// <summary>Load a single conversation by id, or null if missing/corrupt.</summary>
        public static ChatConversation Load(string id)
        {
            try
            {
                var path = PathFor(id);
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<ChatConversation>(File.ReadAllText(path));
            }
            catch (Exception e) { Debug.LogWarning("[Unimancer] Chat load failed: " + e.Message); return null; }
        }

        /// <summary>List all saved conversations, newest first.</summary>
        public static List<ChatConversation> LoadAll()
        {
            var list = new List<ChatConversation>();
            try
            {
                foreach (var file in Directory.GetFiles(Dir(), "*.json"))
                {
                    try
                    {
                        var c = JsonUtility.FromJson<ChatConversation>(File.ReadAllText(file));
                        if (c != null && !string.IsNullOrEmpty(c.id)) list.Add(c);
                    }
                    catch { /* skip a corrupt file */ }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Unimancer] Chat list failed: " + e.Message); }
            list.Sort((a, b) => b.updatedTicks.CompareTo(a.updatedTicks));
            return list;
        }

        /// <summary>Delete a saved conversation.</summary>
        public static void Delete(string id)
        {
            try { var p = PathFor(id); if (File.Exists(p)) File.Delete(p); }
            catch (Exception e) { Debug.LogWarning("[Unimancer] Chat delete failed: " + e.Message); }
        }
    }
}
