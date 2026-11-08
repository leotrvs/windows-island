using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WindowsIsland
{
    // Adds or removes the island's entry in Claude Code's user settings (~/.claude/settings.json): a PermissionRequest
    // hook that asks the island to show Allow and Deny. Only this one entry is ever touched; everything else in the file
    // is kept, a backup of the original is saved beside it, and the file is not changed if it cannot be read.
    static class ClaudeHook
    {
        const string Marker = "permission-hook";
        public const int WaitSeconds = 60;
        const int TimeoutSeconds = WaitSeconds + 20;

        static string ConfigDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                return !string.IsNullOrEmpty(configured) ? configured : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            }
        }

        static string SettingsPath { get { return Path.Combine(ConfigDirectory, "settings.json"); } }

        // Claude Code runs hook commands through a shell, so the path has forward slashes and quotes.
        public static string Command
        {
            // Inside double quotes a shell still expands $ and the backtick, so those are escaped
            get { return "\"" + Installer.StartupTarget.Replace('\\', '/').Replace("$", "\\$").Replace("`", "\\`") + "\" " + Marker; }
        }

        static bool IsOurs(JsonNode entry)
        {
            JsonArray hooks = (entry as JsonObject)?["hooks"] as JsonArray;
            if (hooks == null) return false;
            foreach (JsonNode hook in hooks)
            {
                string command = (hook as JsonObject)?["command"]?.GetValue<string>();
                if (command != null && command.Contains(Marker, StringComparison.Ordinal) && command.Contains("WindowsIsland", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // One entry of ours, with the command and timeout this version would write.
        static bool IsCurrent(JsonArray list)
        {
            int ours = 0;
            foreach (JsonNode entry in list)
            {
                if (!IsOurs(entry)) continue;
                ours++;
                JsonObject hook = ((entry as JsonObject)?["hooks"] as JsonArray)?[0] as JsonObject;
                if (hook == null) return false;
                if (hook["command"]?.GetValue<string>() != Command) return false;
                try { if (hook["timeout"]?.GetValue<int>() != TimeoutSeconds) return false; } catch (Exception) { return false; }
            }
            return ours == 1;
        }

        public static bool IsInstalled
        {
            get
            {
                try
                {
                    if (!File.Exists(SettingsPath)) return false;
                    JsonObject root = Read();
                    JsonArray list = ((root?["hooks"] as JsonObject)?["PermissionRequest"]) as JsonArray;
                    if (list == null) return false;
                    foreach (JsonNode entry in list) if (IsOurs(entry)) return true;
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        static JsonObject Read()
        {
            string text = File.ReadAllText(SettingsPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
            return JsonNode.Parse(text, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }

        // True when the settings file now has (install) or no longer has (remove) the island's hook.
        public static bool Apply(bool install, out string error)
        {
            error = null;
            try
            {
                string path = SettingsPath;
                JsonObject root;
                if (File.Exists(path))
                {
                    root = Read();
                    if (root == null) { error = "settings.json is not a JSON object"; return false; }
                }
                else
                {
                    if (!install) return true;
                    Directory.CreateDirectory(ConfigDirectory);
                    root = new JsonObject();
                }

                JsonObject hooks = root["hooks"] as JsonObject;
                if (hooks == null)
                {
                    if (!install) return true;
                    hooks = new JsonObject();
                    root["hooks"] = hooks;
                }
                JsonArray list = hooks["PermissionRequest"] as JsonArray;
                if (list == null)
                {
                    if (!install) return true;
                    list = new JsonArray();
                    hooks["PermissionRequest"] = list;
                }

                bool changed = false;
                // Already exactly as it should be: the file is not touched (and not reformatted) on every start
                if (install && IsCurrent(list)) return true;
                for (int i = list.Count - 1; i >= 0; i--)
                    if (IsOurs(list[i])) { list.RemoveAt(i); changed = true; }
                if (install)
                {
                    list.Add(new JsonObject
                    {
                        ["matcher"] = "",
                        ["hooks"] = new JsonArray { new JsonObject { ["type"] = "command", ["command"] = Command, ["timeout"] = TimeoutSeconds } },
                    });
                    changed = true;
                }
                else if (list.Count == 0)
                {
                    hooks.Remove("PermissionRequest");
                    if (hooks.Count == 0) root.Remove("hooks");
                }
                if (!changed) return true;

                if (File.Exists(path))
                {
                    string backup = path + ".island-backup"; // The original, kept the first time
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    File.Copy(path, path + ".island-backup-last", true); // And the file as it was before this change
                }
                string temp = path + ".island-tmp";
                File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                error = e is JsonException ? "settings.json could not be read" : "settings.json could not be changed";
                return false;
            }
        }
    }
}
