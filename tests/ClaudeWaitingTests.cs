using System.Collections.Generic;
using Xunit;

namespace WindowsIsland.Tests
{
    public class ClaudeWaitingTests
    {
        static string ToolUse(string id, string tool, string stamp, string input = "{}")
        {
            return "{\"type\":\"assistant\",\"isSidechain\":false,\"timestamp\":\"" + stamp + "\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"id\":\"" + id + "\",\"name\":\"" + tool + "\",\"input\":" + input + "}]}}";
        }

        static string Result(string id)
        {
            return "{\"type\":\"user\",\"isSidechain\":false,\"timestamp\":\"2026-11-08T10:00:05Z\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"" + id + "\",\"content\":\"ok\"}]}}";
        }

        static string Prompt()
        {
            return "{\"type\":\"user\",\"isSidechain\":false,\"timestamp\":\"2026-11-08T10:05:00Z\",\"message\":{\"content\":\"go on\"}}";
        }

        static ClaudeWaiting.Pending Run(out bool asking, out string question, out List<string> buttons, params string[] lines)
        {
            return ClaudeWaiting.Analyze(string.Join("\n", lines), out asking, out question, out buttons);
        }

        [Fact]
        public void AToolCallWithoutAResultIsPending()
        {
            bool asking; string question; List<string> buttons;
            ClaudeWaiting.Pending pending = Run(out asking, out question, out buttons, ToolUse("a1", "Write", "2026-11-08T10:00:00Z"));
            Assert.NotNull(pending);
            Assert.Equal("Write", pending.Name);
        }

        [Fact]
        public void ACallThatGotItsResultIsNotPending()
        {
            bool asking; string question; List<string> buttons;
            ClaudeWaiting.Pending pending = Run(out asking, out question, out buttons, ToolUse("a1", "Bash", "2026-11-08T10:00:00Z"), Result("a1"));
            Assert.Null(pending);
        }

        [Fact]
        public void TheOldestPendingCallIsReported()
        {
            bool asking; string question; List<string> buttons;
            ClaudeWaiting.Pending pending = Run(out asking, out question, out buttons,
                ToolUse("a2", "Edit", "2026-11-08T10:00:09Z"), ToolUse("a1", "Write", "2026-11-08T10:00:01Z"));
            Assert.Equal("Write", pending.Name);
        }

        [Fact]
        public void ASubAgentsCallsAreIgnored()
        {
            bool asking; string question; List<string> buttons;
            string side = ToolUse("s1", "Bash", "2026-11-08T10:00:00Z").Replace("\"isSidechain\":false", "\"isSidechain\":true");
            Assert.Null(Run(out asking, out question, out buttons, side));
        }

        [Fact]
        public void ALineStillBeingWrittenIsSkipped()
        {
            bool asking; string question; List<string> buttons;
            ClaudeWaiting.Pending pending = Run(out asking, out question, out buttons, ToolUse("a1", "Write", "2026-11-08T10:00:00Z"), "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_u");
            Assert.Equal("Write", pending.Name);
        }

        [Fact]
        public void AWrapUpWithAQuestionIsAskingWithItsButtons()
        {
            bool asking; string question; List<string> buttons;
            string input = "{\"recap\":\"done\",\"ask\":\"Publish version 2?\",\"actions\":[{\"label\":\"Yes, publish\"},{\"label\":\"Not yet\"}]}";
            Run(out asking, out question, out buttons, ToolUse("w1", "mcp__ccd_turn__wrap_up", "2026-11-08T10:00:00Z", input), Result("w1"));
            Assert.True(asking);
            Assert.Equal("Publish version 2?", question);
            Assert.Equal(new List<string> { "Yes, publish", "Not yet" }, buttons);
        }

        [Fact]
        public void ABlockingWrapUpWithoutTextStillCounts()
        {
            bool asking; string question; List<string> buttons;
            Run(out asking, out question, out buttons, ToolUse("w1", "mcp__ccd_turn__wrap_up", "2026-11-08T10:00:00Z", "{\"recap\":\"x\",\"blocked\":true}"));
            Assert.True(asking);
        }

        [Fact]
        public void AWrapUpWithoutAQuestionIsNotAsking()
        {
            bool asking; string question; List<string> buttons;
            Run(out asking, out question, out buttons, ToolUse("w1", "mcp__ccd_turn__wrap_up", "2026-11-08T10:00:00Z", "{\"recap\":\"all done\"}"));
            Assert.False(asking);
        }

        [Fact]
        public void WhatTheUserSaysAfterwardsAnswersTheQuestion()
        {
            bool asking; string question; List<string> buttons;
            string input = "{\"recap\":\"x\",\"ask\":\"Go?\"}";
            Run(out asking, out question, out buttons, ToolUse("w1", "mcp__ccd_turn__wrap_up", "2026-11-08T10:00:00Z", input), Result("w1"), Prompt());
            Assert.False(asking);
            Assert.Null(question);
        }

        [Fact]
        public void AToolResultAfterTheQuestionDoesNotAnswerIt()
        {
            bool asking; string question; List<string> buttons;
            string input = "{\"recap\":\"x\",\"ask\":\"Go?\"}";
            Run(out asking, out question, out buttons, ToolUse("w1", "mcp__ccd_turn__wrap_up", "2026-11-08T10:00:00Z", input), Result("w1"), Result("zz"));
            Assert.True(asking);
        }

        [Fact]
        public void ALongQuestionIsShortened()
        {
            bool asking; string question; List<string> buttons;
            string input = "{\"ask\":\"" + new string('a', 300) + "\"}";
            Run(out asking, out question, out buttons, ToolUse("w1", "mcp__ccd_turn__wrap_up", "2026-11-08T10:00:00Z", input));
            Assert.Equal(140, question.Length);
            Assert.EndsWith("…", question);
        }
    }
}
