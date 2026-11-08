using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchServicePossibleMatchesTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _service = new SeasonMatchService(_context, _dataPullService, Substitute.For<IBracketService>(), new GrifballWebApp.Server.Championships.ChampionshipService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetPossibleMatches_ReturnsEmpty_When_AwayTeamHasNoPlayers()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var lonely = new Team { SeasonID = seeded.Season.SeasonID, TeamName = "Empty" };
        _context.Teams.Add(lonely);
        await _context.SaveChangesAsync();
        var sm = SeasonMatch.Seed(seeded.Season.SeasonID, seeded.Home.TeamID, lonely.TeamID, bestOf: 1);
        _context.SeasonMatches.Add(sm);
        await _context.SaveChangesAsync();

        var result = await _service.GetPossibleMatches(sm.SeasonMatchID);

        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task GetPossibleMatches_ReturnsEmpty_When_PlayersHaveNoXboxAccount()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, playersHaveXbox: false);

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task GetPossibleMatches_ReadsTheDatabaseOnly_AndFiltersCandidates()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var h = SeasonMatchTestData.HomeXbox;
        var a = SeasonMatchTestData.AwayXbox;
        var o = SeasonMatchTestData.OtherXbox;
        var start = new DateTime(2025, 3, 1, 20, 0, 0, DateTimeKind.Utc);
        await SeasonAround(seeded, start);

        // Valid, home players on team 1 (swapped)
        var valid1 = await SeasonMatchTestData.AddMatchAsync(_context, start, a, Outcomes.Lost, h, Outcomes.Won, team0Score: 2, team1Score: 7);
        // Valid with a ringer on the away side, later start time
        var valid2 = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(1), h, Outcomes.Won, [a[0], a[1], a[2], o[0]], Outcomes.Lost);
        // Excluded: no winner
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(2), h, Outcomes.Tie, a, Outcomes.Tie);
        // Excluded: team sizes are not 4v4
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(3), h[..3], Outcomes.Won, a, Outcomes.Lost);
        // Excluded: already linked to a season match
        var linked = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(4), h, Outcomes.Won, a, Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { MatchID = linked.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 });
        await _context.SaveChangesAsync();
        // Excluded by GetTeams: only one known player, other team all strangers
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(5), [h[0], o[1], o[2], o[3]], Outcomes.Won, o[4..], Outcomes.Lost);
        // Excluded by GetTeams: mixed teams
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(6), [h[0], h[1], a[0], a[1]], Outcomes.Won, [h[2], h[3], a[2], a[3]], Outcomes.Lost);
        // Excluded: strangers only
        await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(7), o[..4], Outcomes.Won, o[4..], Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        // The players' games are pulled by a job (SeasonMatchPullJob, over these players), not by the page
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
        var players = await _service.PlayerXboxIDs(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(players.Count == 8 && h.All(players.Contains) && a.All(players.Contains), Is.True);

        Assert.That(result.Select(x => x.MatchID), Is.EqualTo(new[] { valid2.MatchID, valid1.MatchID }), "ordered by start time desc");

        var first = result[1]; // valid1
        Assert.Multiple(() =>
        {
            Assert.That(first.HomeTeam.TeamID, Is.EqualTo(1));
            Assert.That(first.HomeTeam.Score, Is.EqualTo(7));
            Assert.That(first.HomeTeam.Outcome, Is.EqualTo(Outcomes.Won));
            Assert.That(first.HomeTeam.Players.Select(p => p.XboxUserID), Is.EquivalentTo(h));
            Assert.That(first.HomeTeam.Players.All(p => p.IsOnTeam), Is.True);
            Assert.That(first.AwayTeam.TeamID, Is.EqualTo(0));
            Assert.That(first.AwayTeam.Score, Is.EqualTo(2));
            Assert.That(first.AwayTeam.Outcome, Is.EqualTo(Outcomes.Lost));
            var p201 = first.AwayTeam.Players.Single(p => p.XboxUserID == 201);
            Assert.That(p201.Gamertag, Is.EqualTo("GT201"));
            Assert.That(p201.Kills, Is.EqualTo(1));
            Assert.That(p201.Deaths, Is.EqualTo(1));
            Assert.That(p201.Score, Is.EqualTo(1));
        });

        var second = result[0]; // valid2, ringer o[0]
        Assert.Multiple(() =>
        {
            Assert.That(second.HomeTeam.TeamID, Is.EqualTo(0));
            Assert.That(second.AwayTeam.Players.Single(p => p.XboxUserID == o[0]).IsOnTeam, Is.False);
            Assert.That(second.AwayTeam.Players.Count(p => p.IsOnTeam), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetPossibleMatches_WithMoreThan200Candidates_ReturnsTheNewest200()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var start = new DateTime(2025, 3, 1, 20, 0, 0, DateTimeKind.Utc);
        await SeasonAround(seeded, start);
        for (var i = 0; i < 200; i++)
            _context.Matches.Add(Candidate(start.AddMinutes(i)));
        var newest = Candidate(start.AddDays(1));
        _context.Matches.Add(newest);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(200));
            Assert.That(result[0].MatchID, Is.EqualTo(newest.MatchID), "The newest is never the one cut");
            Assert.That(result.Select(x => x.MatchID), Does.Not.Contain(MatchAt(start)), "The oldest is");
        });

        Guid MatchAt(DateTime time) => _context.Matches.Single(x => x.StartTime == time).MatchID;
    }

    /// <summary>The seeded season moved to run from <paramref name="start"/> for two weeks, where the test's games are.</summary>
    private async Task SeasonAround(SeasonMatchTestData.Seeded seeded, DateTime start)
    {
        var season = await _context.Seasons.SingleAsync(s => s.SeasonID == seeded.SeasonMatch.SeasonID);
        season.WithDates(seasonStart: start, seasonEnd: start.AddDays(14));
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task GetPossibleMatches_AreTheSeasonsGames_FromADayBeforeItToAWeekAfter()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var seasonStart = new DateTime(2024, 4, 14, 16, 0, 0, DateTimeKind.Utc);
        await SeasonAround(seeded, seasonStart);
        var seasonEnd = seasonStart.AddDays(14);
        var tooEarly = Candidate(seasonStart.AddDays(-2));
        var dayBefore = Candidate(seasonStart.AddHours(-12));
        var during = Candidate(seasonStart.AddDays(3));
        var weekAfter = Candidate(seasonEnd.AddDays(6));
        var tooLate = Candidate(seasonEnd.AddDays(8));
        // Played long after: an old match's newest games, which used to crowd out its own
        var muchLater = Candidate(seasonStart.AddYears(2));
        _context.Matches.AddRange(tooEarly, dayBefore, during, weekAfter, tooLate, muchLater);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(result.Select(x => x.MatchID), Is.EqualTo(new[] { weekAfter.MatchID, during.MatchID, dayBefore.MatchID }));
        var window = await _service.SeasonWindow(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(window, Is.EqualTo(((DateTime From, DateTime To)?)(seasonStart.AddDays(-1), seasonEnd.AddDays(7))));
        Assert.That(await _service.SeasonWindow(-1), Is.Null, "No such match");
    }

    [Test]
    public async Task GetPossibleMatches_ReachesAMatchRescheduledPastTheSeason_ToADayAfterItsTime()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var seasonStart = new DateTime(2024, 4, 14, 16, 0, 0, DateTimeKind.Utc);
        await SeasonAround(seeded, seasonStart);
        // Rescheduled (RescheduleService) to three weeks after the season's end, past the week after it
        var rescheduled = seasonStart.AddDays(14 + 21);
        (await _context.SeasonMatches.SingleAsync(sm => sm.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID)).ScheduleAt(rescheduled);
        var played = Candidate(rescheduled.AddHours(20));
        var tooLate = Candidate(rescheduled.AddDays(2));
        _context.Matches.AddRange(played, tooLate);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);
        var window = await _service.SeasonWindow(seeded.SeasonMatch.SeasonMatchID);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(x => x.MatchID), Is.EqualTo(new[] { played.MatchID }));
            Assert.That(window, Is.EqualTo(((DateTime From, DateTime To)?)(seasonStart.AddDays(-1), rescheduled.AddDays(1))));
        });
    }

    [Test]
    public async Task SeasonWindow_KeepsTheWeekAfterTheSeason_ForAMatchScheduledWithinIt()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var seasonStart = new DateTime(2024, 4, 14, 16, 0, 0, DateTimeKind.Utc);
        await SeasonAround(seeded, seasonStart);
        // Scheduled on the season's last day: a day after it is still within the week after the season
        (await _context.SeasonMatches.SingleAsync(sm => sm.SeasonMatchID == seeded.SeasonMatch.SeasonMatchID)).ScheduleAt(seasonStart.AddDays(14));
        await _context.SaveChangesAsync();

        var window = await _service.SeasonWindow(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(window, Is.EqualTo(((DateTime From, DateTime To)?)(seasonStart.AddDays(-1), seasonStart.AddDays(14 + 7))));
    }

    [Test]
    public async Task GetPossibleMatches_OnlyCountsGamesWithBothTeamsPlayers_TowardsTheNewest200()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var start = new DateTime(2024, 4, 14, 16, 0, 0, DateTimeKind.Utc);
        await SeasonAround(seeded, start);
        // The match's game, then 200 newer ones with only the home team's players (their scrims against strangers)
        var theMatch = Candidate(start.AddDays(1));
        _context.Matches.Add(theMatch);
        for (var i = 0; i < 200; i++)
            _context.Matches.Add(Candidate(start.AddDays(2).AddMinutes(i), SeasonMatchTestData.OtherXbox[..4]));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(result.Select(x => x.MatchID), Is.EqualTo(new[] { theMatch.MatchID }), "Not pushed out by games it couldn't be");
    }

    /// <param name="away">The other side's players: the away team's unless given.</param>
    private static Match Candidate(DateTime start, long[]? away = null)
    {
        var match = Match.Seed(Guid.NewGuid(), startTime: start, endTime: start.AddMinutes(10), duration: TimeSpan.FromMinutes(10));
        void Team(int teamId, long[] players, Outcomes outcome)
        {
            match.AddTeam(teamId, 1, outcome);
            foreach (var x in players)
                match.AddParticipant(teamId, x, new MatchParticipantStats());
        }
        Team(0, SeasonMatchTestData.HomeXbox, Outcomes.Won);
        Team(1, away ?? SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        return match;
    }
}
