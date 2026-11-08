using System.Collections.Generic;
using Xunit;

namespace WindowsIsland.Tests
{
    public class SteamManifestTests
    {
        static string Manifest(string flags, long toDownload, long downloaded, long toStage, long staged)
        {
            return "\"AppState\"\n{\n\t\"appid\"\t\t\"4262460\"\n\t\"name\"\t\t\"ROB RUN\"\n\t\"StateFlags\"\t\t\"" + flags + "\"\n" +
                   "\t\"BytesToDownload\"\t\t\"" + toDownload + "\"\n\t\"BytesDownloaded\"\t\t\"" + downloaded + "\"\n" +
                   "\t\"BytesToStage\"\t\t\"" + toStage + "\"\n\t\"BytesStaged\"\t\t\"" + staged + "\"\n" +
                   "\t\"InstalledDepots\"\n\t{\n\t\t\"4262461\"\n\t\t{\n\t\t\t\"manifest\"\t\t\"2796912922969855489\"\n\t\t\t\"size\"\t\t\"5080613225\"\n\t\t}\n\t}\n}\n";
        }

        [Fact]
        public void TheNameAndTheNumbersAreRead()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("1026", 1000, 250, 2000, 0));
            Assert.Equal("4262460", m.AppId);
            Assert.Equal("ROB RUN", m.Name);
            Assert.Equal(1026, m.StateFlags);
            Assert.Equal(1000, m.BytesToDownload);
            Assert.Equal(250, m.BytesDownloaded);
        }

        [Fact]
        public void ADownloadInProgressShowsItsPercent()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("1026", 1000, 250, 2000, 0));
            Assert.True(m.IsWorking);
            Assert.False(m.IsInstalling);
            Assert.Equal(25, m.Progress);
        }

        [Fact]
        public void WhenTheDownloadIsDoneTheFilesBeingPutInPlaceShowTheProgress()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("2097154", 1000, 1000, 2000, 500));
            Assert.True(m.IsWorking);
            Assert.True(m.IsInstalling);
            Assert.Equal(25, m.Progress);
        }

        [Fact]
        public void AnInstalledGameIsNotWorking()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("4", 1000, 1000, 2000, 2000));
            Assert.False(m.IsWorking);
            Assert.False(m.IsPaused);
        }

        [Fact]
        public void APausedDownloadIsPausedNotWorking()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("1538", 1000, 400, 2000, 0)); // 2 + 512 + 1024
            Assert.False(m.IsWorking);
            Assert.True(m.IsPaused);
        }

        [Fact]
        public void ThePercentNeverReachesAHundredBeforeItIsDone()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("1026", 1000, 999, 2000, 0));
            Assert.Equal(99, m.Progress);
        }

        [Fact]
        public void WithoutAnyByteCountsTheProgressIsUnknown()
        {
            SteamManifest m = SteamManifest.Parse(Manifest("1026", 0, 0, 0, 0));
            Assert.Equal(-1, m.Progress);
        }

        [Fact]
        public void AFileWithoutAnAppIdOrTooBigIsRefused()
        {
            Assert.Null(SteamManifest.Parse("\"AppState\"\n{\n}\n"));
            Assert.Null(SteamManifest.Parse(null));
            Assert.Null(SteamManifest.Parse(new string('a', 300 * 1024)));
        }

        [Fact]
        public void TheLibrariesAreReadFromTheFolderList()
        {
            string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t\t\"label\"\t\t\"\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t}\n}\n";
            List<string> libraries = SteamManifest.Libraries(vdf);
            Assert.Equal(new List<string> { "C:\\Program Files (x86)\\Steam", "D:\\SteamLibrary" }, libraries);
        }

        [Fact]
        public void TheLogSaysWhatRunsAndWhatIsPaused()
        {
            string log = "[t] AppID 252950 state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,\n" +
                         "[t] AppID 252950 state changed : Update Required,Fully Installed,Update Queued,Update Started, (Suspended)\n" +
                         "[t] AppID 526870 state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,\n" +
                         "[t] AppID 447040 state changed : Fully Installed,\n";
            Dictionary<string, string> states = SteamManifest.States(log);
            Assert.False(SteamManifest.StateIsRunning(states["252950"]));
            Assert.True(SteamManifest.StateIsRunning(states["526870"]));
            Assert.False(SteamManifest.StateIsRunning(states["447040"]));
            Assert.True(SteamManifest.StateIsDone(states["447040"]));
            Assert.False(SteamManifest.StateIsDone(states["252950"]));
        }

        [Fact]
        public void BetweenManifestWritesTheProgressCarriesOnButNotFar()
        {
            Assert.Equal(10, SteamManifest.Estimate(1000, 10000, 100, 0));
            Assert.Equal(15, SteamManifest.Estimate(1000, 10000, 100, 5));
            Assert.Equal(15, SteamManifest.Estimate(1000, 10000, 100, 500)); // never more than a twentieth ahead
            Assert.Equal(10, SteamManifest.Estimate(1000, 10000, -5, 10));
            Assert.Equal(-1, SteamManifest.Estimate(0, 0, 1, 1));
        }
    }
}
