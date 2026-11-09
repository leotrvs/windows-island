using System.Collections.Generic;
using Xunit;

namespace WindowsIsland.Tests
{
    public class FootballFeedTests
    {
        const string Sample = "{\"events\":[" +
            "{\"id\":\"1\",\"status\":{\"displayClock\":\"34'\",\"type\":{\"state\":\"in\"}},\"competitions\":[{\"competitors\":[" +
            "{\"homeAway\":\"home\",\"score\":\"1\",\"team\":{\"displayName\":\"Flamengo\",\"abbreviation\":\"FLA\"}}," +
            "{\"homeAway\":\"away\",\"score\":\"0\",\"team\":{\"displayName\":\"Palmeiras\",\"abbreviation\":\"PAL\"}}]}]}," +
            "{\"id\":\"2\",\"status\":{\"displayClock\":\"90'+5'\",\"type\":{\"state\":\"post\"}},\"competitions\":[{\"competitors\":[" +
            "{\"homeAway\":\"home\",\"score\":\"0\",\"team\":{\"displayName\":\"Vitória\",\"abbreviation\":\"VIT\"}}," +
            "{\"homeAway\":\"away\",\"score\":\"3\",\"team\":{\"displayName\":\"Fluminense\",\"abbreviation\":\"FLU\"}}]}]}]}";

        [Fact]
        public void TheMatchesAreRead()
        {
            List<FootballMatch> matches = FootballFeed.Parse(Sample);
            Assert.Equal(2, matches.Count);
            Assert.True(matches[0].Live);
            Assert.Equal("FLA 1 x 0 PAL", matches[0].Line);
            Assert.Equal("34'", matches[0].Clock);
            Assert.True(matches[1].Finished);
            Assert.Equal(3, matches[1].Goals);
        }

        [Fact]
        public void AMatchIsFoundByNameAbbreviationOrPartOfTheName()
        {
            FootballMatch match = FootballFeed.Parse(Sample)[0];
            Assert.True(FootballFeed.Has(match, "flamengo"));
            Assert.True(FootballFeed.Has(match, "PAL"));
            Assert.True(FootballFeed.Has(match, "palme"));
            Assert.False(FootballFeed.Has(match, "Santos"));
            Assert.False(FootballFeed.Has(match, "F"));
            Assert.False(FootballFeed.Has(match, ""));
        }

        [Fact]
        public void AccentsDoNotGetInTheWay()
        {
            Assert.True(FootballFeed.Has(FootballFeed.Parse(Sample)[1], "vitoria"));
        }

        [Fact]
        public void NonsenseGivesNoMatches()
        {
            Assert.Empty(FootballFeed.Parse(null));
            Assert.Empty(FootballFeed.Parse("not json"));
            Assert.Empty(FootballFeed.Parse("{\"events\":5}"));
        }

        [Fact]
        public void OnlyValidLeagueCodesAreKeptWithAtMostFive()
        {
            Assert.Equal(new List<string> { "bra.1", "eng.1", "uefa.champions" }, FootballFeed.Leagues("bra.1, ENG.1 ../x uefa.champions bra.1"));
            Assert.Equal(5, FootballFeed.Leagues("a1 b1 c1 d1 e1 f1 g1").Count);
            Assert.Empty(FootballFeed.Leagues(""));
        }
    }
}
