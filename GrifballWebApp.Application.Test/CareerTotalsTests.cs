using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.Test;

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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(StatMath.KdRatio(10, 3), Is.EqualTo(3.33m));
            Assert.That(StatMath.KdRatio(10, 0), Is.Null);
            Assert.That(StatMath.WinPercent(2, 3), Is.EqualTo(66.7m));
            Assert.That(StatMath.WinPercent(0, 0), Is.Null);
            Assert.That(StatMath.PerGame(7, 4), Is.EqualTo(1.75m));
            Assert.That(StatMath.PerGame(7, 0), Is.Null);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => g.GameVersionID), Is.EqualTo(new int?[] { 1, 6 }));
            Assert.That((games[0].SeasonsCount, games[0].GamesPlayed, games[0].Wins, games[0].Goals), Is.EqualTo((2, 20, 10, (int?)10)));
            Assert.That((games[0].WinPercent, games[0].KdRatio, games[0].KdSpread), Is.EqualTo(((decimal?)50m, (decimal?)1.45m, 25)));
            Assert.That(games[0].GoalsPerGame, Is.EqualTo(0.5m));
            Assert.That(games[0].StopsPerGame, Is.EqualTo(0.6m), "6 stops over the 10 games that record them");
            Assert.That(games[1].KdRatio, Is.Null, "No deaths");
        }
    }

    [Test]
    public void AcrossGames_TotalsWhatEveryGameCountsAlike_AndGoalsAndStops()
    {
        var career = CareerTotals.AcrossGames([Line(1, 1, 10, 6, 50, 25, goals: 8, stops: 4), Line(3, 6, 5, 5, 40, 20, goals: 9, stops: 3, recordsStops: false)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((career.GameVersionID, career.GameVersionSlug, career.SeasonsCount), Is.EqualTo(((int?)null, "all", 2)));
            Assert.That((career.GamesPlayed, career.Wins, career.Losses, career.Kills, career.Deaths), Is.EqualTo((15, 11, 4, 90, 45)));
            Assert.That((career.KdRatio, career.WinPercent), Is.EqualTo(((decimal?)2m, (decimal?)73.3m)));
            Assert.That((career.Goals, career.GoalsPerGame), Is.EqualTo(((long?)17, (decimal?)1.13m)), "17 goals over 15 games, whatever the game");
            Assert.That((career.CarrierKills, career.StopsPerGame), Is.EqualTo(((int?)7, (decimal?)0.4m)), "4 stops over the 10 games that record them");
            Assert.That((career.Sprees, career.HammerKills), Is.EqualTo(((int?)null, (int?)null)), "Medals stay per game");
        }
    }

    [Test]
    public void TeamRecord_FromTheSheet_IsTheSheets_NotCountedFromMatches()
    {
        var record = TeamRecord.Of.Compile();
        var team = Team.Seed(teamID: 1, teamName: "A", recordedWins: 5, recordedLosses: 0);
        // Lost at home: in the team's home matches, as EF would load it
        SeasonMatch.Seed(null, team, Team.Seed(teamID: 2, teamName: "B")).WonBy(SeasonMatchSide.Away);

        var fromSheet = record(team);
        team.ClearArchivedRecord();
        var fromMatches = record(team);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((fromSheet.Wins, fromSheet.Losses, fromSheet.FromSource), Is.EqualTo((5, 0, true)), "The sheet's 0 losses, not the loss of its matches");
            Assert.That((fromMatches.Wins, fromMatches.Losses, fromMatches.FromSource), Is.EqualTo((0, 1, false)));
        }
    }

    [Test]
    public void SeasonInfo_Select_SetsTheSeasonsFieldsAndTheDerivedDtosOwn()
    {
        var season = Season.Seed(seasonID: 4, seasonName: "Summer League 2010", historicalSourceID: "wb/sml10", historicalSourceUrl: "https://x", league: League.Seed("AGLA", "agla"), format: SeasonFormat.LeagueSeason).WithDates(seasonStart: new DateTime(2010, 1, 1), seasonEnd: new DateTime(2010, 12, 31));

        var item = SeasonInfoDto.Select(DateTime.UtcNow, s => new SeasonListItemDto { TeamCount = 3 }).Compile()(season);
        // Ended at its end exactly, not before
        var atEnd = SeasonInfoDto.Of(season.SeasonEnd).Compile()(season);
        var justBefore = SeasonInfoDto.Of(season.SeasonEnd.AddTicks(-1)).Compile()(season);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((item.SeasonID, item.SeasonName, item.Year, item.HasEnded), Is.EqualTo((4, "Summer League 2010", 2010, true)));
            Assert.That((item.LeagueName, item.IsHistorical, item.HistoricalSourceUrl, item.GameVersionName), Is.EqualTo(("AGLA", true, "https://x", (string?)null)));
            Assert.That(item.TeamCount, Is.EqualTo(3));
            Assert.That((atEnd.HasEnded, justBefore.HasEnded), Is.EqualTo((true, false)));
        }
        Assert.Throws<ArgumentException>(() => SeasonInfoDto.Select<SeasonListItemDto>(DateTime.UtcNow, s => null!));
    }
}
