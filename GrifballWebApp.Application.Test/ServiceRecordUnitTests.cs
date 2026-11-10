using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;

namespace GrifballWebApp.Application.Test;

/// <summary>ServiceRecordTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ServiceRecordUnitTests
{
    [Test]
    public void Totals_AddUpTheLines_AndSayHowManyGamesGoalsCouldntBeRead()
    {
        SeasonStatLine Line(int played, int wins, int kills, int deaths, long goals) => new()
        {
            Season = new SeasonInfoDto { SeasonID = 1 },
            Gamertag = "x",
            GamesPlayed = played,
            Wins = wins,
            Losses = played - wins,
            Kills = kills,
            Deaths = deaths,
            Goals = goals,
        };

        // The lines' goals already leave out a game whose goals can't be read (PlayerGameStatsQuery)
        var totals = ServiceRecord.Totals([Line(3, 2, 30, 10, 4), Line(2, 0, 5, 10, 1)], 1);
        var empty = ServiceRecord.Totals([], 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((totals.GamesPlayed, totals.Wins, totals.Losses, totals.Kills, totals.Deaths), Is.EqualTo((5, 2, 3, 35, 20)));
            Assert.That((totals.KdRatio, totals.WinPercent), Is.EqualTo(((decimal?)1.75m, (decimal?)40.0m)));
            Assert.That((totals.BombScores, totals.UncountedBombScoreGames), Is.EqualTo((5L, 1)));
            Assert.That((empty.GamesPlayed, empty.KdRatio, empty.WinPercent, empty.BombScores), Is.EqualTo((0, (decimal?)null, (decimal?)null, 0L)));
        }
    }

    [Test]
    public void Combine_AddsTheCounts_AndWorksOutTheRatiosAgain()
    {
        var a = new ServiceRecordTotalsDto { GamesPlayed = 3, Wins = 2, Losses = 1, Kills = 30, Deaths = 10, BombScores = 4, UncountedBombScoreGames = 1 };
        var b = new ServiceRecordTotalsDto { GamesPlayed = 2, Wins = 0, Losses = 2, Kills = 5, Deaths = 10, BombScores = 1 };

        var both = ServiceRecord.Combine(a, b);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((both.GamesPlayed, both.Wins, both.Losses, both.Kills, both.Deaths), Is.EqualTo((5, 2, 3, 35, 20)));
            Assert.That((both.KdRatio, both.WinPercent), Is.EqualTo(((decimal?)1.75m, (decimal?)40.0m)));
            Assert.That((both.BombScores, both.UncountedBombScoreGames), Is.EqualTo((5L, 1)));
        }
    }
}
