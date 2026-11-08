using System;
using System.IO;
using Xunit;

namespace WindowsIsland.Tests
{
    // The hook editor works on a file named by an environment variable, so these tests run one at a time.
    [Collection("ClaudeConfig")]
    public class ClaudeHookTests : IDisposable
    {
        readonly string folder;
        readonly string file;
        readonly string previous;

        public ClaudeHookTests()
        {
            folder = Path.Combine(Path.GetTempPath(), "island-hook-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, "settings.json");
            previous = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", folder);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", previous);
            try { Directory.Delete(folder, true); } catch (Exception) { }
        }

        const string Mine = "{ \"model\": \"opus\", \"hooks\": { \"PreToolUse\": [ { \"matcher\": \"Bash\", \"hooks\": [ { \"type\": \"command\", \"command\": \"echo hi\" } ] } ] } }";

        [Fact]
        public void InstallingAddsOneHookAndKeepsEverythingElse()
        {
            File.WriteAllText(file, Mine);
            string error;
            Assert.True(ClaudeHook.Apply(true, out error));
            Assert.True(ClaudeHook.IsInstalled);
            string text = File.ReadAllText(file);
            Assert.Contains("\"model\": \"opus\"", text);
            Assert.Contains("echo hi", text);
            Assert.Contains("permission-hook", text);
        }

        [Fact]
        public void InstallingTwiceStillLeavesOneEntry()
        {
            File.WriteAllText(file, Mine);
            string error;
            ClaudeHook.Apply(true, out error);
            ClaudeHook.Apply(true, out error);
            string text = File.ReadAllText(file);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "permission-hook"));
        }

        [Fact]
        public void InstallingWhenAlreadyCurrentDoesNotTouchTheFile()
        {
            File.WriteAllText(file, Mine);
            string error;
            ClaudeHook.Apply(true, out error);
            string once = File.ReadAllText(file);
            // A hand-made change that does not involve our entry must survive another "install"
            File.WriteAllText(file, once.Replace("\"opus\"", "\"sonnet\""));
            ClaudeHook.Apply(true, out error);
            Assert.Contains("\"sonnet\"", File.ReadAllText(file));
        }

        [Fact]
        public void EveryChangeKeepsTheLastVersionAsABackup()
        {
            File.WriteAllText(file, Mine);
            string error;
            ClaudeHook.Apply(true, out error);
            string installed = File.ReadAllText(file);
            ClaudeHook.Apply(false, out error);
            Assert.Equal(installed, File.ReadAllText(file + ".island-backup-last"));
        }

        [Fact]
        public void RemovingTakesOnlyOurEntryAway()
        {
            File.WriteAllText(file, Mine);
            string error;
            ClaudeHook.Apply(true, out error);
            Assert.True(ClaudeHook.Apply(false, out error));
            Assert.False(ClaudeHook.IsInstalled);
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("permission-hook", text);
            Assert.Contains("echo hi", text);
            Assert.DoesNotContain("PermissionRequest", text);
        }

        [Fact]
        public void ABackupOfTheOriginalIsKept()
        {
            File.WriteAllText(file, Mine);
            string error;
            ClaudeHook.Apply(true, out error);
            Assert.Equal(Mine, File.ReadAllText(file + ".island-backup"));
        }

        [Fact]
        public void AFileThatCannotBeReadIsLeftAlone()
        {
            File.WriteAllText(file, "{ not json");
            string error;
            Assert.False(ClaudeHook.Apply(true, out error));
            Assert.NotNull(error);
            Assert.Equal("{ not json", File.ReadAllText(file));
        }

        [Fact]
        public void CommentsAndTrailingCommasAreAccepted()
        {
            File.WriteAllText(file, "{\n  // my note\n  \"model\": \"opus\",\n}\n");
            string error;
            Assert.True(ClaudeHook.Apply(true, out error));
            Assert.Contains("opus", File.ReadAllText(file));
        }

        [Fact]
        public void WithNoFileRemovingDoesNothingAndInstallingCreatesIt()
        {
            string error;
            Assert.True(ClaudeHook.Apply(false, out error));
            Assert.False(File.Exists(file));
            Assert.True(ClaudeHook.Apply(true, out error));
            Assert.True(File.Exists(file));
        }

        [Fact]
        public void TheCommandUsesForwardSlashesAndQuotes()
        {
            Assert.StartsWith("\"D:/Apps/WindowsIsland/WindowsIsland.exe\" ", ClaudeHook.Command);
            Assert.EndsWith(" permission-hook", ClaudeHook.Command);
        }
    }
}
