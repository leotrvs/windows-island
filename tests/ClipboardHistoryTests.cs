using System;
using Xunit;

namespace WindowsIsland.Tests
{
    public class ClipboardHistoryTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 10, 12, 0, 0);

        [Fact]
        public void KeepsAtMostFiveNewestFirst()
        {
            ClipboardHistory history = new ClipboardHistory();
            for (int i = 1; i <= 7; i++) Assert.True(history.Add("text " + i, T0.AddSeconds(i)));
            Assert.Equal(ClipboardHistory.MaxItems, history.Items.Count);
            Assert.Equal("text 7", history.Items[0].Text);
            Assert.Equal("text 3", history.Items[4].Text);
        }

        [Fact]
        public void RefusesEmptySpacesAndTooLong()
        {
            ClipboardHistory history = new ClipboardHistory();
            Assert.False(history.Add(null, T0));
            Assert.False(history.Add("", T0));
            Assert.False(history.Add("   \r\n\t ", T0));
            Assert.False(history.Add(new string('a', ClipboardHistory.MaxLength + 1), T0));
            Assert.True(history.Add(new string('a', ClipboardHistory.MaxLength), T0));
            Assert.Single(history.Items);
        }

        [Fact]
        public void TheSameAsTheTopIsIgnoredAndAnOlderOneMovesUp()
        {
            ClipboardHistory history = new ClipboardHistory();
            history.Add("a", T0);
            history.Add("b", T0.AddSeconds(1));
            Assert.False(history.Add("b", T0.AddSeconds(2)));
            Assert.Equal(T0.AddSeconds(1), history.Items[0].Copied);
            Assert.True(history.Add("a", T0.AddSeconds(3)));
            Assert.Equal(2, history.Items.Count);
            Assert.Equal("a", history.Items[0].Text);
        }

        [Fact]
        public void CopyingBackMovesToTheTopWithoutADuplicate()
        {
            ClipboardHistory history = new ClipboardHistory();
            history.Add("a", T0);
            history.Add("b", T0.AddSeconds(1));
            history.Add("c", T0.AddSeconds(2));
            Assert.True(history.Promote("a", T0.AddSeconds(5)));
            Assert.Equal(3, history.Items.Count);
            Assert.Equal("a", history.Items[0].Text);
            Assert.Equal(T0.AddSeconds(5), history.Items[0].Copied);
            Assert.False(history.Promote("not there", T0));
        }

        [Fact]
        public void ForgetsAfterTheChosenTime()
        {
            ClipboardHistory history = new ClipboardHistory();
            history.Add("old", T0);
            history.Add("new", T0.AddMinutes(4));
            Assert.False(history.Expire(T0.AddMinutes(4.9), TimeSpan.FromMinutes(5)));
            Assert.True(history.Expire(T0.AddMinutes(5), TimeSpan.FromMinutes(5)));
            Assert.Single(history.Items);
            Assert.Equal("new", history.Items[0].Text);
            Assert.True(history.Expire(T0.AddMinutes(10), TimeSpan.FromMinutes(5)));
            Assert.Empty(history.Items);
        }

        [Fact]
        public void ClearEmptiesTheList()
        {
            ClipboardHistory history = new ClipboardHistory();
            history.Add("a", T0);
            Assert.True(history.Clear());
            Assert.Empty(history.Items);
            Assert.False(history.Clear());
        }

        [Theory]
        [InlineData(true, false, null, null, true)]
        [InlineData(false, true, null, null, true)]
        [InlineData(false, false, 0, null, true)]
        [InlineData(false, false, null, 0, true)]
        [InlineData(false, false, 1, 1, false)]
        [InlineData(false, false, null, null, false)]
        public void PrivateFormatsAreRefused(bool exclude, bool viewerIgnore, int? history, int? cloud, bool refused)
        {
            Assert.Equal(refused, ClipboardHistory.Refused(exclude, viewerIgnore, history, cloud));
        }

        [Theory]
        [InlineData("1Password", true)]
        [InlineData("Bitwarden", true)]
        [InlineData("KeePass", true)]
        [InlineData("KeePassXC", true)]
        [InlineData("LastPass", true)]
        [InlineData("Dashlane", true)]
        [InlineData("NordPass", true)]
        [InlineData("Proton Pass", true)]
        [InlineData("Enpass", true)]
        [InlineData("RoboForm", true)]
        [InlineData("notepad", false)]
        [InlineData("chrome", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void PasswordManagersAreKnown(string process, bool expected)
        {
            Assert.Equal(expected, ClipboardHistory.IsPasswordManager(process));
        }

        [Fact]
        public void OneLineJoinsLinesAndCuts()
        {
            Assert.Equal("first second third", ClipboardHistory.OneLine("first\r\nsecond\n\tthird  "));
            string line = ClipboardHistory.OneLine(new string('x', 100));
            Assert.Equal(ClipboardHistory.LineLength, line.Length);
            Assert.EndsWith("…", line);
            Assert.Equal("short", ClipboardHistory.OneLine("short"));
            Assert.Equal("", ClipboardHistory.OneLine(null));
        }
    }
}
