using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.MatchPlanner;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>Row history's helpers, which work on either database: SQL Server's temporal tables, Postgres's history tables.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RowHistoryQueryTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static bool KeepsHistoryInTheApp => TestDatabase.Provider == DatabaseProvider.Postgres;

    private async Task<int> HistoryCount<TEntity>() where TEntity : class
    {
        await using var read = _context.NewContextLike();
        return await read.HistoryOf<TEntity>().CountAsync();
    }

    /// <summary>A moment between two saves, on either database's clock.</summary>
    private static async Task<DateTime> Moment()
    {
        await Task.Delay(200);
        var moment = DateTime.UtcNow;
        await Task.Delay(200);
        return moment;
    }

    [Test]
    public async Task AsOf_ReadsTheRowsAsTheyWereThen()
    {
        var beforeInsert = await Moment();
        var team = new Team { TeamName = "Alpha", Season = new Season { SeasonName = "Season" } };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        var asAlpha = await Moment();
        team.TeamName = "Bravo";
        await _context.SaveChangesAsync();
        var asBravo = await Moment();
        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();
        var afterDelete = await Moment();

        async Task<List<string>> NamesAsOf(DateTime moment) =>
            await _context.AsOf<Team>(moment).Where(t => t.TeamID == team.TeamID).Select(t => t.TeamName).ToListAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await NamesAsOf(beforeInsert), Is.Empty);
            Assert.That(await NamesAsOf(asAlpha), Is.EqualTo(new[] { "Alpha" }));
            Assert.That(await NamesAsOf(asBravo), Is.EqualTo(new[] { "Bravo" }));
            Assert.That(await NamesAsOf(afterDelete), Is.Empty);
            Assert.That(_context.AsOf<Team>(asAlpha).Single().SeasonID, Is.EqualTo(team.SeasonID));
        });
    }

    private async Task<Season> SeasonWithMatches(int count, bool bracket)
    {
        var season = new Season { SeasonName = "Season" };
        for (var i = 0; i < count; i++)
        {
            season.SeasonMatches.Add(new SeasonMatch
            {
                BestOf = 3,
                BracketMatch = bracket ? new MatchBracketInfo { MatchNumber = i + 1, RoundNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2 } : null,
            });
        }
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return season;
    }

    [Test]
    public async Task ExecuteDeleteWithHistory_Deletes_RecordingWhatItDeletes()
    {
        var season = await SeasonWithMatches(3, bracket: true);

        var deleted = await _context.SeasonMatches.Where(m => m.SeasonID == season.SeasonID && m.BestOf == 3)
            .ExecuteDeleteWithHistoryAsync(_context);

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.EqualTo(3));
            Assert.That(await _context.SeasonMatches.CountAsync(), Is.Zero);
            Assert.That(await _context.MatchBracketInfo.CountAsync(), Is.Zero);
            if (KeepsHistoryInTheApp)
            {
                Assert.That(await HistoryCount<SeasonMatch>(), Is.EqualTo(3));
                Assert.That(await HistoryCount<MatchBracketInfo>(), Is.EqualTo(3));
            }
        });
    }

    [Test]
    public async Task MatchPlanner_ReplacingMatches_RecordsTheOldOnes()
    {
        var season = new Season { SeasonName = "Season" };
        season.Teams.Add(new Team { TeamName = "Alpha" });
        season.Teams.Add(new Team { TeamName = "Bravo" });
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var service = new MatchPlannerService(_context);

        await service.CreateSeasonMatches(season.SeasonID, 1, 3);
        _context.ChangeTracker.Clear();
        await service.CreateSeasonMatches(season.SeasonID, 1, 5);

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.SeasonMatches.Select(m => m.BestOf).ToListAsync(), Is.EqualTo(new[] { 5, 5 }));
            if (KeepsHistoryInTheApp)
                Assert.That(await HistoryCount<SeasonMatch>(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Bracket_ReplacingTheBracket_RecordsTheOldOne()
    {
        var season = new Season { SeasonName = "Season" };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var service = new BracketService(_context, new TeamStandingsService(_context));

        await service.CreateBracket(4, season.SeasonID, doubleElimination: false, bestOf: 3);
        _context.ChangeTracker.Clear(); // the old bracket read from the database, as in a new request
        await service.CreateBracket(4, season.SeasonID, doubleElimination: false, bestOf: 5);

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.SeasonMatches.Select(m => m.BestOf).ToListAsync(), Has.Count.EqualTo(3).And.All.EqualTo(5));
            if (KeepsHistoryInTheApp)
            {
                Assert.That(await HistoryCount<SeasonMatch>(), Is.EqualTo(3));
                Assert.That(await HistoryCount<MatchBracketInfo>(), Is.EqualTo(3));
            }
        });
    }
}
