using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.TeamStandings;

namespace GrifballWebApp.Test;

/// <summary>The totals and ratios a profile and the leaderboards share, the team record rule, and the season projection.</summary>
[TestFixture]
public class CareerTotalsTests
{
    private static SeasonStatLine Line(int seasonID, int? game, int played, int wins, int kills, int deaths, int goals = 0, int stops = 0, bool recordsStops = true) => new()
    {
        Season = new SeasonInfoDto { SeasonID = seasonID },
        GameVersionID = game,
        GameVersionName = game is null ? null : $"Game {game}",
        Gamertag = "x",
        GamesPlayed = played,
        Wins = wins,
        Losses = played - wins,
        Kills = kills,
        Deaths = deaths,
        Goals = goals,
        CarrierKills = stops,
        RecordsCarrierKills = recordsStops,
    };

    [Test]
    public void StatMath_RoundsOneWay_AndIsNullWithoutADenominator()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StatMath.KdRatio(10, 3), Is.EqualTo(3.33m));
            Assert.That(StatMath.KdRatio(10, 0), Is.Null);
            Assert.That(StatMath.WinPercent(2, 3), Is.EqualTo(66.7m));
            Assert.That(StatMath.WinPercent(0, 0), Is.Null);
            Assert.That(StatMath.PerGame(7, 4), Is.EqualTo(1.75m));
            Assert.That(StatMath.PerGame(7, 0), Is.Null);
        });
    }

    [Test]
    public void ByGame_TotalsEachGame_AndCarrierStopsPerGameOnlyOverTheLinesThatRecordThem()
    {
        var lines = new[]
        {
            Line(1, 1, played: 10, wins: 6, kills: 50, deaths: 25, goals: 8, stops: 6),
            Line(2, 1, played: 10, wins: 4, kills: 30, deaths: 30, goals: 2, stops: 0, recordsStops: false),
            Line(3, 6, played: 5, wins: 5, kills: 40, deaths: 0, goals: 9),
        };

        var games = CareerTotals.ByGame(lines);

        Assert.Multiple(() =>
        {
            Assert.That(games.Select(g => g.GameVersionID), Is.EqualTo(new int?[] { 1, 6 }));
            Assert.That((games[0].SeasonsCount, games[0].GamesPlayed, games[0].Wins, games[0].Goals), Is.EqualTo((2, 20, 10, (int?)10)));
            Assert.That((games[0].WinPercent, games[0].KdRatio, games[0].KdSpread), Is.EqualTo(((decimal?)50m, (decimal?)1.45m, 25)));
            Assert.That(games[0].GoalsPerGame, Is.EqualTo(0.5m));
            Assert.That(games[0].StopsPerGame, Is.EqualTo(0.6m), "6 stops over the 10 games that record them");
            Assert.That(games[1].KdRatio, Is.Null, "No deaths");
        });
    }

    [Test]
    public void AcrossGames_TotalsOnlyWhatEveryGameCountsAlike()
    {
        var career = CareerTotals.AcrossGames([Line(1, 1, 10, 6, 50, 25, goals: 8), Line(3, 6, 5, 5, 40, 20, goals: 9)]);

        Assert.Multiple(() =>
        {
            Assert.That((career.GameVersionID, career.GameVersionSlug, career.SeasonsCount), Is.EqualTo(((int?)null, "all", 2)));
            Assert.That((career.GamesPlayed, career.Wins, career.Losses, career.Kills, career.Deaths), Is.EqualTo((15, 11, 4, 90, 45)));
            Assert.That((career.KdRatio, career.WinPercent), Is.EqualTo(((decimal?)2m, (decimal?)73.3m)));
            Assert.That((career.Goals, career.CarrierKills), Is.EqualTo(((int?)null, (int?)null)), "Goals and stops are per game");
        });
    }

    [Test]
    public void TeamRecord_FromTheSheet_TakesBothHalvesFromIt_EvenWhenOneIsBlank()
    {
        var record = TeamRecord.Of.Compile();
        var team = new Team { TeamID = 1, TeamName = "A", RecordedWins = 5 };
        // Lost at home: in the team's home matches, as EF would load it
        SeasonMatch.Seed(null, team, new Team { TeamID = 2, TeamName = "B" }).WonBy(SeasonMatchSide.Away);

        var fromSheet = record(team);
        team.RecordedWins = null;
        var fromMatches = record(team);

        Assert.Multiple(() =>
        {
            Assert.That((fromSheet.Wins, fromSheet.Losses, fromSheet.FromSource), Is.EqualTo((5, 0, true)), "The blank half is 0, not counted from matches");
            Assert.That((fromMatches.Wins, fromMatches.Losses, fromMatches.FromSource), Is.EqualTo((0, 1, false)));
        });
    }

    [Test]
    public void SeasonInfo_Select_SetsTheSeasonsFieldsAndTheDerivedDtosOwn()
    {
        var season = new Season
        {
            SeasonID = 4, SeasonName = "Summer League 2010", HistoricalSourceID = "wb/sml10", HistoricalSourceUrl = "https://x",
            SeasonStart = new DateTime(2010, 1, 1), SeasonEnd = new DateTime(2010, 12, 31),
            League = new League { LeagueName = "AGLA", Slug = "agla" }, Format = SeasonFormat.LeagueSeason,
        };

        var item = SeasonInfoDto.Select(s => new SeasonListItemDto { TeamCount = 3 }).Compile()(season);

        Assert.Multiple(() =>
        {
            Assert.That((item.SeasonID, item.SeasonName, item.Year, item.HasEnded), Is.EqualTo((4, "Summer League 2010", 2010, true)));
            Assert.That((item.LeagueName, item.IsHistorical, item.HistoricalSourceUrl, item.GameVersionName), Is.EqualTo(("AGLA", true, "https://x", (string?)null)));
            Assert.That(item.TeamCount, Is.EqualTo(3));
        });
        Assert.Throws<ArgumentException>(() => SeasonInfoDto.Select<SeasonListItemDto>(s => null!));
    }
}
