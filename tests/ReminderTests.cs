using System;
using System.IO;
using Xunit;

namespace WindowsIsland.Tests
{
    public class ReminderParserTests
    {
        // Tuesday 2026-11-10, 10:00
        static readonly DateTime Now = new DateTime(2026, 11, 10, 10, 0, 0);

        static DateTime Today(int h, int m = 0) { return Now.Date.AddHours(h).AddMinutes(m); }

        [Theory]
        [InlineData("ligar pro João 15h", "ligar pro João", 15, 0)]
        [InlineData("15h ligar pro João", "ligar pro João", 15, 0)]
        [InlineData("ligar pro João 15h30", "ligar pro João", 15, 30)]
        [InlineData("ligar pro João 15:30", "ligar pro João", 15, 30)]
        [InlineData("call John 3pm", "call John", 15, 0)]
        [InlineData("call John at 3:45 pm", "call John", 15, 45)]
        [InlineData("ligar pro João às 15", "ligar pro João", 15, 0)]
        [InlineData("às 15 ligar pro João", "ligar pro João", 15, 0)]
        [InlineData("llamar a Juan a las 17", "llamar a Juan", 17, 0)]
        [InlineData("llamar a Juan 17:15", "llamar a Juan", 17, 15)]
        [InlineData("позвонить Ивану в 18:30", "позвонить Ивану", 18, 30)]
        [InlineData("в 18 позвонить Ивану", "позвонить Ивану", 18, 0)]
        [InlineData("позвонить Ивану 20 часов", "позвонить Ивану", 20, 0)]
        [InlineData("Reunião às 14h30, sala 2", "Reunião, sala 2", 14, 30)]
        [InlineData("lembre-me de pagar a conta 18h", "pagar a conta", 18, 0)]
        [InlineData("remind me to stretch 4 pm", "stretch", 16, 0)]
        public void AbsoluteTodayWhenStillAhead(string input, string text, int hour, int minute)
        {
            ReminderParser.Result r = ReminderParser.Parse(input, Now);
            Assert.True(r.Ok, input);
            Assert.Equal(text, r.Text);
            DateTime expected = Today(hour, minute);
            if (expected <= Now) expected = expected.AddDays(1);
            Assert.Equal(expected, r.When);
        }

        [Fact]
        public void ATimeAlreadyGoneTodayMeansTomorrow()
        {
            ReminderParser.Result r = ReminderParser.Parse("alongar 9h", Now);
            Assert.True(r.Ok);
            Assert.Equal(Now.Date.AddDays(1).AddHours(9), r.When);
        }

        [Theory]
        [InlineData("ligar pro João em 20 min", 20)]
        [InlineData("em 20 minutos ligar pro João", 20)]
        [InlineData("ligar pro João daqui a 2 horas", 120)]
        [InlineData("call John in 20 minutes", 20)]
        [InlineData("call John in 2 hours", 120)]
        [InlineData("call John in an hour", 60)]
        [InlineData("llamar a Juan en 20 minutos", 20)]
        [InlineData("llamar a Juan en 1 hora", 60)]
        [InlineData("позвонить Ивану через 20 минут", 20)]
        [InlineData("через 2 часа позвонить Ивану", 120)]
        [InlineData("em 5 min", 5)]
        public void Relative(string input, int minutes)
        {
            ReminderParser.Result r = ReminderParser.Parse(input, Now);
            Assert.True(r.Ok, input);
            Assert.Equal(Now.AddMinutes(minutes), r.When);
            if (r.Text != input) Assert.DoesNotContain("min", r.Text.ToLowerInvariant()); // the time words are cut out
        }

        [Theory]
        [InlineData("amanhã 9h reunião", "reunião", 9, 0)]
        [InlineData("reunião amanhã às 9", "reunião", 9, 0)]
        [InlineData("tomorrow 9am dentist", "dentist", 9, 0)]
        [InlineData("dentist tomorrow at 9:30 am", "dentist", 9, 30)]
        [InlineData("mañana 9h dentista", "dentista", 9, 0)]
        [InlineData("завтра 9:00 встреча", "встреча", 9, 0)]
        [InlineData("tomorrow 3pm call", "call", 15, 0)]
        public void Tomorrow(string input, string text, int hour, int minute)
        {
            ReminderParser.Result r = ReminderParser.Parse(input, Now);
            Assert.True(r.Ok, input);
            Assert.Equal(text, r.Text);
            Assert.Equal(Now.Date.AddDays(1).AddHours(hour).AddMinutes(minute), r.When);
        }

        [Fact]
        public void TodayWord()
        {
            ReminderParser.Result r = ReminderParser.Parse("hoje 18h treino", Now);
            Assert.True(r.Ok);
            Assert.Equal("treino", r.Text);
            Assert.Equal(Today(18), r.When);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ligar pro João")]
        [InlineData("comprar 3 pães")]
        [InlineData("amanhã reunião")]
        [InlineData("25h reunião")]
        [InlineData("reunião 14:75")]
        [InlineData("13pm teste")]
        [InlineData("em 0 min")]
        public void NoTimeNoReminder(string input)
        {
            Assert.False(ReminderParser.Parse(input, Now).Ok, input);
        }

        [Fact]
        public void OnlyATimeKeepsTheWholeText()
        {
            ReminderParser.Result r = ReminderParser.Parse("15h", Now);
            Assert.True(r.Ok);
            Assert.Equal("15h", r.Text);
        }

        [Fact]
        public void TwelveHourEdges()
        {
            Assert.Equal(Now.Date.AddDays(1), ReminderParser.Parse("x 12am", Now).When);
            Assert.Equal(Today(12), ReminderParser.Parse("x 12pm", Now).When);
        }
    }

    public class ReminderStoreTests : IDisposable
    {
        readonly string folder = Path.Combine(Path.GetTempPath(), "island-reminders-" + Guid.NewGuid().ToString("N"));
        string FilePath { get { return Path.Combine(folder, "reminders.json"); } }
        static readonly DateTime T0 = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        [Fact]
        public void SavedAndLoadedByAnotherInstance()
        {
            ReminderStore a = new ReminderStore(FilePath);
            a.Add("second", T0.AddHours(2));
            a.Add("first", T0.AddHours(1));
            Assert.True(File.Exists(FilePath));
            Assert.False(File.Exists(FilePath + ".tmp"));

            ReminderStore b = new ReminderStore(FilePath);
            b.Load();
            Assert.Equal(2, b.Items.Count);
            Assert.Equal("first", b.Items[0].Text); // soonest first
            Assert.Equal(T0.AddHours(1), b.Items[0].DueUtc);
        }

        [Fact]
        public void RemoveIsSaved()
        {
            ReminderStore a = new ReminderStore(FilePath);
            ReminderStore.Item item = a.Add("x", T0);
            a.Add("y", T0.AddMinutes(1));
            Assert.True(a.Remove(item.Id));
            Assert.False(a.Remove("nope"));
            ReminderStore b = new ReminderStore(FilePath);
            b.Load();
            Assert.Single(b.Items);
            Assert.Equal("y", b.Items[0].Text);
        }

        [Fact]
        public void TakeDueRemovesOnlyThoseDue()
        {
            ReminderStore a = new ReminderStore(FilePath);
            a.Add("past", T0.AddMinutes(-5));
            a.Add("later", T0.AddMinutes(30));
            System.Collections.Generic.List<ReminderStore.Item> due = a.TakeDue(T0);
            Assert.Single(due);
            Assert.Equal("past", due[0].Text);
            ReminderStore b = new ReminderStore(FilePath);
            b.Load();
            Assert.Single(b.Items);
            Assert.Equal("later", b.Items[0].Text);
        }

        [Fact]
        public void BrokenOrMissingFileIsEmptyAndNeverThrows()
        {
            ReminderStore a = new ReminderStore(FilePath);
            a.Load();
            Assert.Empty(a.Items);
            Directory.CreateDirectory(folder);
            File.WriteAllText(FilePath, "{ not json");
            a.Load();
            Assert.Empty(a.Items);
            Assert.NotNull(a.Add("works again", T0));
        }

        [Fact]
        public void RefusesEmptyAndLimitsLength()
        {
            ReminderStore a = new ReminderStore(FilePath);
            Assert.Null(a.Add("  ", T0));
            ReminderStore.Item item = a.Add(new string('a', ReminderStore.MaxLength + 50), T0);
            Assert.Equal(ReminderStore.MaxLength, item.Text.Length);
        }

        [Fact]
        public void UnwritablePathIsSwallowed()
        {
            ReminderStore a = new ReminderStore(Path.Combine(folder, "reminders.json", "x", "y.json"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "reminders.json"), "file where a folder should be");
            Assert.NotNull(a.Add("still in memory", T0));
            Assert.Single(a.Items);
        }
    }

    public class DownloadTrackerTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 10, 12, 0, 0);
        const string File1 = @"D:\Downloads\big.iso";

        [Theory]
        [InlineData(@"C:\d\a.crdownload", true)]
        [InlineData(@"C:\d\a.PART", true)]
        [InlineData(@"C:\d\a.opdownload", true)]
        [InlineData(@"C:\d\a.download", true)]
        [InlineData(@"C:\d\a.tmp", true)]
        [InlineData(@"C:\d\a.partial", true)]
        [InlineData(@"C:\d\a.iso", false)]
        [InlineData(@"C:\d\a.part.zip", false)]
        public void PartialNames(string path, bool partial)
        {
            Assert.Equal(partial, DownloadTracker.IsPartialName(path));
        }

        [Theory]
        [InlineData(512L, "512 B")]
        [InlineData(850L * 1024, "850 KB")]
        [InlineData(230L * 1024 * 1024, "230 MB")]
        [InlineData(1536L * 1024 * 1024, "1.5 GB")]
        public void Sizes(long bytes, string text)
        {
            Assert.Equal(text, DownloadTracker.FormatSize(bytes));
        }

        [Fact]
        public void ThresholdAndShortName()
        {
            Assert.True(DownloadTracker.MeetsMinimum(50L * 1024 * 1024, 50));
            Assert.False(DownloadTracker.MeetsMinimum(50L * 1024 * 1024 - 1, 50));
            Assert.Equal("short.zip", DownloadTracker.ShortName("short.zip", 20));
            string cut = DownloadTracker.ShortName("a-very-long-installer-name-for-testing.exe", 20);
            Assert.Equal(20, cut.Length);
            Assert.EndsWith("….exe", cut);
        }

        [Fact]
        public void RenameFromPartialReportsOnce()
        {
            DownloadTracker t = new DownloadTracker();
            Assert.True(t.Renamed(@"C:\d\f.zip.crdownload", @"C:\d\f.zip", T0));
            Assert.False(t.Renamed(@"C:\d\f.zip.crdownload", @"C:\d\f.zip", T0.AddSeconds(1)));
            Assert.False(t.Renamed(@"C:\d\a.txt", @"C:\d\b.txt", T0)); // an ordinary rename
            Assert.False(t.Renamed(@"C:\d\g.zip.crdownload", @"C:\d\g.zip.part", T0)); // still partial
        }

        [Fact]
        public void GrowingThenStableFor3SecondsReports()
        {
            DownloadTracker t = new DownloadTracker();
            long size = 100;
            t.Changed(File1, size, T0);
            size = 5000;
            t.Changed(File1, size, T0.AddSeconds(1));
            Func<string, long> sizeOf = p => size;
            Func<string, bool> open = p => true;
            Assert.Empty(t.Stable(T0.AddSeconds(3), sizeOf, open));            // only 2 s since it changed
            var done = t.Stable(T0.AddSeconds(4), sizeOf, open);              // 3 s
            Assert.Single(done);
            Assert.Equal(File1, done[0].Key);
            Assert.Equal(5000, done[0].Value);
            Assert.Empty(t.Stable(T0.AddSeconds(20), sizeOf, open));          // never twice
            t.Changed(File1, 5000, T0.AddSeconds(21));
            Assert.Equal(0, t.Count);
        }

        [Fact]
        public void StillGrowingOrLockedIsNotReported()
        {
            DownloadTracker t = new DownloadTracker();
            long size = 10;
            t.Changed(File1, size, T0);
            size = 20;
            Assert.Empty(t.Stable(T0.AddSeconds(5), p => size, p => true)); // grew since the last look: the clock restarts
            Assert.Empty(t.Stable(T0.AddSeconds(9), p => size, p => false)); // stable but locked
            Assert.Single(t.Stable(T0.AddSeconds(10), p => size, p => true)); // unlocked
        }

        [Fact]
        public void AFileThatNeverGrewIsIgnored()
        {
            DownloadTracker t = new DownloadTracker();
            t.Changed(File1, 999, T0);
            Assert.Empty(t.Stable(T0.AddSeconds(10), p => 999, p => true));
        }

        [Fact]
        public void PartialAndMissingFilesAreNotTracked()
        {
            DownloadTracker t = new DownloadTracker();
            t.Changed(@"C:\d\a.crdownload", 10, T0);
            Assert.Equal(0, t.Count);
            t.Changed(File1, 10, T0);
            Assert.Empty(t.Stable(T0.AddSeconds(10), p => -1, p => true));
            Assert.Equal(0, t.Count);
        }
    }
}
