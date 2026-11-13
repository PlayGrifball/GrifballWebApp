using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class StatLineAndGameTotalsUnitTests
{
    private static SeasonStatLine NewLine() => new() { Season = new SeasonInfoDto { SeasonID = 1, SeasonName = "S1" }, Gamertag = "" };

    [Test]
    public void SetStored_UsesStoredRatiosWhenGiven()
    {
        var stored = PlayerSeasonStat.Seed(new StatSheetTotals
        {
            Gamertag = "Chief117",
            GamesPlayed = 10,
            Wins = 7,
            Losses = 3,
            WinPercent = 70m,
            Kills = 100,
            Deaths = 40,
            KdRatio = 2.5m,
            KdSpread = 60,
            Goals = 5,
            CarrierKills = 3,
            Betrayals = 1,
        }, gameVersionID: 6, teamID: 10);

        var line = NewLine();
        line.SetStored(stored, "Halo Infinite", "infinite");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.Gamertag, Is.EqualTo("Chief117"));
            Assert.That(line.GameVersionName, Is.EqualTo("Halo Infinite"));
            Assert.That(line.GameVersionSlug, Is.EqualTo("infinite"));
            Assert.That(line.GamesPlayed, Is.EqualTo(10));
            Assert.That(line.WinPercent, Is.EqualTo(70m));
            Assert.That(line.KdRatio, Is.EqualTo(2.5m));
            Assert.That(line.KdSpread, Is.EqualTo(60));
            Assert.That(line.FromMatches, Is.False);
        }
    }

    [Test]
    public void SetStored_ComputesRatiosWhenNotGiven()
    {
        var stored = PlayerSeasonStat.Seed(new StatSheetTotals
        {
            Gamertag = "Chief117",
            GamesPlayed = 4,
            Wins = 2,
            Losses = 2,
            Kills = 20,
            Deaths = 10,
        }, gameVersionID: 6);

        var line = NewLine();
        line.SetStored(stored, null, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.WinPercent, Is.EqualTo(50m));
            Assert.That(line.KdRatio, Is.EqualTo(2m));
            Assert.That(line.KdSpread, Is.EqualTo(10));
        }
    }

    [Test]
    public void SetGames_TotalsFromGamesAndMarksUnrecordedStatsNull()
    {
        var gameVersion = GameVersion.Seed("Halo Infinite", "infinite", gameVersionID: 6);
        var totals = new GameTotals
        {
            UserID = 1,
            Played = 5,
            DecidedByVote = 1,
            Wins = 3,
            Losses = 2,
            Kills = 50,
            Deaths = 25,
            Assists = 10,
            Goals = 7,
            CarrierKills = 4,
            RecordingCarrierKills = 5,
            HammerKills = 0,
            RecordingHammerKills = 0, // never recorded by any game: stays null
            SwordKills = 2,
            RecordingSwordKills = 3,
        };

        var line = NewLine();
        line.SetGames(totals, gameVersion);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.FromMatches, Is.True);
            Assert.That(line.GameVersionName, Is.EqualTo("Halo Infinite"));
            Assert.That(line.GamesPlayed, Is.EqualTo(5));
            Assert.That(line.GamesDecidedByVote, Is.EqualTo(1));
            Assert.That(line.GamesWithStats, Is.EqualTo(4));
            Assert.That(line.WinPercent, Is.EqualTo(60m));
            Assert.That(line.KdRatio, Is.EqualTo(2m));
            Assert.That(line.HammerKills, Is.Null);
            Assert.That(line.SwordKills, Is.EqualTo(2));
            Assert.That(line.RecordsCarrierKills, Is.True);
        }
    }

    [Test]
    public void SetGames_NoGamesRecordCarrierKills_RecordsCarrierKillsIsFalse()
    {
        var totals = new GameTotals { UserID = 1, Played = 3, RecordingCarrierKills = 0 };

        var line = NewLine();
        line.SetGames(totals, null);

        Assert.That(line.RecordsCarrierKills, Is.False);
    }

    [Test]
    public void GameTotals_Of_GroupsAndSumsByPlayerSeasonPhaseAndTeam()
    {
        var games = new List<PlayerGameStat>
        {
            new() { SeasonID = 1, UserID = 1, IsPlayoffs = false, TeamID = 10, Gamertag = "Chief117", GameVersionID = 6, Won = true, Kills = 10, Deaths = 5, Goals = 2 },
            new() { SeasonID = 1, UserID = 1, IsPlayoffs = false, TeamID = 10, Gamertag = "Chief117", GameVersionID = 6, Lost = true, Kills = 5, Deaths = 8, Goals = 1 },
            new() { SeasonID = 1, UserID = 1, IsPlayoffs = true, TeamID = 10, Gamertag = "Chief117", GameVersionID = 6, Won = true, Kills = 7, Deaths = 2 },
            new() { SeasonID = 1, UserID = 2, IsPlayoffs = false, TeamID = 20, Gamertag = "Other", GameVersionID = 6, Won = true, Kills = 3, Deaths = 1 },
        }.AsQueryable();

        var totals = GameTotals.Of(games).OrderBy(t => t.UserID).ThenBy(t => t.IsPlayoffs).ToList();

        Assert.That(totals, Has.Count.EqualTo(3));
        var regularSeason = totals[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(regularSeason.UserID, Is.EqualTo(1));
            Assert.That(regularSeason.IsPlayoffs, Is.False);
            Assert.That(regularSeason.Played, Is.EqualTo(2));
            Assert.That(regularSeason.Wins, Is.EqualTo(1));
            Assert.That(regularSeason.Losses, Is.EqualTo(1));
            Assert.That(regularSeason.Kills, Is.EqualTo(15));
            Assert.That(regularSeason.Deaths, Is.EqualTo(13));
            Assert.That(regularSeason.Goals, Is.EqualTo(3));
        }
        var playoffs = totals[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(playoffs.UserID, Is.EqualTo(1));
            Assert.That(playoffs.IsPlayoffs, Is.True);
            Assert.That(playoffs.Played, Is.EqualTo(1));
            Assert.That(playoffs.Kills, Is.EqualTo(7));
        }
    }
}
