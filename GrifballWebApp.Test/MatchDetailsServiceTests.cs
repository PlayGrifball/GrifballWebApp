using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.MatchDetails;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchDetailsServiceTests
{
    private GrifballContext _context;
    private MatchDetailsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new MatchDetailsService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetAsync_ReturnsNull_When_MatchNotPulled()
    {
        Assert.That(await _service.GetAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task GetAsync_ReadsGoals_AsGrifballGoalsDoes_AndHidesPackedPoints()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var start = new DateTime(2026, 10, 30, 23, 0, 0, DateTimeKind.Utc);
        // Two custom-mode games packing their counts into the scores (#217): one decoded (2 goals), one not yet
        var decoded = await SeasonMatchTestData.AddMatchAsync(_context, start, [301, 302], Outcomes.Won, [303, 304], Outcomes.Lost);
        var undecoded = await SeasonMatchTestData.AddMatchAsync(_context, start.AddHours(1), [301, 302], Outcomes.Won, [303, 304], Outcomes.Lost);
        // Halo's raw values, as a pull saves them; the decoded game's layout and goals as the decoder leaves them
        var inDecoded = (await _context.MatchParticipants.FindAsync(decoded.MatchID, 302L))!;
        _context.Entry(inDecoded).Property(p => p.Score).CurrentValue = 504_010_048;
        _context.Entry(inDecoded).Property(p => p.PersonalScore).CurrentValue = 504_010_048;
        _context.Entry(inDecoded).Property(p => p.Goals).CurrentValue = 2;
        _context.Entry((await _context.Matches.FindAsync(decoded.MatchID))!).Property(m => m.ScoreLayout).CurrentValue = CustomScoreLayout.B;
        var inUndecoded = (await _context.MatchParticipants.FindAsync(undecoded.MatchID, 302L))!;
        _context.Entry(inUndecoded).Property(p => p.Score).CurrentValue = 790_240_328;
        _context.Entry(inUndecoded).Property(p => p.PersonalScore).CurrentValue = 790_240_328;
        await _context.SaveChangesAsync();

        var decodedLine = (await _service.GetAsync(decoded.MatchID))!.Teams[0].Players.Single(p => p.Gamertag == "GT302");
        var undecodedLine = (await _service.GetAsync(undecoded.MatchID))!.Teams[0].Players.Single(p => p.Gamertag == "GT302");

        Assert.Multiple(() =>
        {
            Assert.That((decodedLine.Goals, decodedLine.PersonalScore), Is.EqualTo(((int?)2, (int?)null)), "Decoded: its goals; the points are packed");
            Assert.That((undecodedLine.Goals, undecodedLine.PersonalScore), Is.EqualTo(((int?)null, (int?)790_240_328)), "Not decoded yet: no goals read from a packed Score");
        });
    }

    [Test]
    public async Task GetAsync_ReturnsEveryStat_ForAMatchNotReported()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        await GradesTestData.SeedMedals(_context);
        var start = new DateTime(2026, 10, 30, 23, 0, 0, DateTimeKind.Utc);
        // Players with an Xbox account but no user of the site
        var match = Match.Seed(Guid.NewGuid(), startTime: start, endTime: start.AddMinutes(10), duration: TimeSpan.FromMinutes(10));
        match.AddTeam(0, 6, Outcomes.Won);
        match.AddTeam(1, 2, Outcomes.Lost);
        match.AddParticipant(0, 301, new MatchParticipantStats { Score = 1, Kills = 1, Deaths = 1 }, [new MedalCount(3, 1)]);
        match.AddParticipant(0, 302, new MatchParticipantStats
        {
            Score = 2, Kills = 2, Deaths = 1, Assists = 4, PersonalScore = 1200, Kda = 3.5f, Accuracy = 55.5f, ShotsFired = 20, ShotsHit = 11,
            DamageDealt = 900, DamageTaken = 300, MeleeKills = 2, AverageLife = TimeSpan.FromSeconds(42), TimePlayed = TimeSpan.FromMinutes(10),
            LeftInProgress = true,
        }, [new MedalCount(3, 2), new MedalCount(6, 1)]);
        match.AddParticipant(1, 303, new MatchParticipantStats { Kills = 3, Deaths = 2 }, [new MedalCount(7, 1)]);
        match.AddParticipant(1, 304, new MatchParticipantStats { Kills = 4, Deaths = 2 });
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();

        var details = await _service.GetAsync(match.MatchID);

        Assert.That(details, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(details!.MatchID, Is.EqualTo(match.MatchID));
            Assert.That(details.StartTime, Is.EqualTo(start));
            Assert.That(details.Duration, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(details.SeasonMatch, Is.Null);
            Assert.That(details.Teams.Select(t => t.TeamID), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(details.Teams.Select(t => t.Score), Is.EqualTo(new[] { 6, 2 }));
            Assert.That(details.Teams.Select(t => t.Outcome), Is.EqualTo(new[] { Outcomes.Won, Outcomes.Lost }));
            Assert.That(details.Teams.All(t => t.SeasonTeamID == null && t.SeasonTeamName == null), Is.True);
        });

        // Most goals first (a Score of 2 against 1)
        var team0 = details!.Teams[0].Players;
        Assert.That(team0.Select(p => p.Gamertag), Is.EqualTo(new[] { "GT302", "GT301" }));
        var player = team0[0];
        Assert.Multiple(() =>
        {
            Assert.That(player.UserID, Is.Null);
            Assert.That(player.Goals, Is.EqualTo(2));
            Assert.That(player.Kills, Is.EqualTo(2));
            Assert.That(player.Deaths, Is.EqualTo(1));
            Assert.That(player.Assists, Is.EqualTo(4));
            Assert.That(player.PersonalScore, Is.EqualTo(1200));
            Assert.That(player.Kda, Is.EqualTo(3.5f));
            Assert.That(player.Accuracy, Is.EqualTo(55.5f));
            Assert.That(player.ShotsFired, Is.EqualTo(20));
            Assert.That(player.ShotsHit, Is.EqualTo(11));
            Assert.That(player.DamageDealt, Is.EqualTo(900));
            Assert.That(player.DamageTaken, Is.EqualTo(300));
            Assert.That(player.MeleeKills, Is.EqualTo(2));
            Assert.That(player.AverageLife, Is.EqualTo(TimeSpan.FromSeconds(42)));
            Assert.That(player.TimePlayed, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(player.LeftInProgress, Is.True);
            Assert.That(player.Medals.Select(m => (m.Name, m.Count)), Is.EqualTo(new[] { ("Double Kill", 2), ("Grand Slam", 1) }));
            Assert.That(player.Medals[0].Type, Is.EqualTo("Multikill"));
            Assert.That(player.Medals[0].Difficulty, Is.EqualTo("Normal"));
            Assert.That(player.Medals[0].Description, Is.EqualTo("Double Kill"));
            Assert.That(details.Teams[1].Players.Single(p => p.Gamertag == "GT304").Medals, Is.Empty);
        });

        // The whole match's medals, summed over the players: most earned first
        Assert.That(details.Medals.Select(m => (m.MedalID, m.Count)), Is.EqualTo(new[] { (3L, 3), (6L, 1), (7L, 1) }));
    }

    [Test]
    public async Task GetAsync_NamesEachTeamAfterTheSeasonTeamItsPlayersAreOn_ForAReportedGame()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 5);
        // Infinite's team 0 is the away team's players (one ringer), team 1 the home team's
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow,
            [201, 202, 203, 301], Outcomes.Lost, [101, 102, 103, 104], Outcomes.Won, team0Score: 3, team1Score: 5);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2, MatchID = match.MatchID });
        await _context.SaveChangesAsync();

        var details = await _service.GetAsync(match.MatchID);

        Assert.That(details, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(details!.SeasonMatch, Is.EqualTo(new MatchDetailsSeasonMatchDto
            {
                SeasonMatchID = seeded.SeasonMatch.SeasonMatchID,
                SeasonID = seeded.Season.SeasonID,
                SeasonName = "Cov Season",
                GameNumber = 2,
                BestOf = 5,
            }));
            Assert.That(details.Teams[0].SeasonTeamID, Is.EqualTo(seeded.Away.TeamID));
            Assert.That(details.Teams[0].SeasonTeamName, Is.EqualTo("Away Team"));
            Assert.That(details.Teams[1].SeasonTeamID, Is.EqualTo(seeded.Home.TeamID));
            Assert.That(details.Teams[1].SeasonTeamName, Is.EqualTo("Home Team"));
            // The rostered players link to their profiles; the ringer has no user
            Assert.That(details.Teams[1].Players.All(p => p.UserID != null), Is.True);
            Assert.That(details.Teams[0].Players.Single(p => p.Gamertag == "GT301").UserID, Is.Null);
            Assert.That(details.Medals, Is.Empty);
        });
    }

    [Test]
    public async Task GetAsync_LeavesATeamUnnamed_When_ItsPlayersAreSplitEvenly()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow,
            [101, 201], Outcomes.Won, [301, 302], Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID });
        await _context.SaveChangesAsync();

        var details = await _service.GetAsync(match.MatchID);

        Assert.Multiple(() =>
        {
            Assert.That(details!.SeasonMatch, Is.Not.Null);
            Assert.That(details.Teams.All(t => t.SeasonTeamID == null && t.SeasonTeamName == null), Is.True);
        });
    }

    [Test]
    public async Task GetAsync_NamesTheLessHomeLeaningTeamAway_When_BothTeamsLeanHome()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        // Both lobbies are mostly home players: team 0 leads by one, team 1 by two. As the box score and scoring the
        // series decide it (SeriesSides), team 1 is home and team 0 the other side
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow,
            [101, 102, 201], Outcomes.Won, [103, 104], Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID });
        await _context.SaveChangesAsync();

        var details = await _service.GetAsync(match.MatchID);

        Assert.Multiple(() =>
        {
            Assert.That(details!.Teams[0].SeasonTeamID, Is.EqualTo(seeded.Away.TeamID));
            Assert.That(details.Teams[0].SeasonTeamName, Is.EqualTo("Away Team"));
            Assert.That(details.Teams[1].SeasonTeamID, Is.EqualTo(seeded.Home.TeamID));
            Assert.That(details.Teams[1].SeasonTeamName, Is.EqualTo("Home Team"));
        });
    }

    [Test]
    public async Task GetAsync_LeavesBothTeamsUnnamed_When_TheyLeanTheSameWayEqually()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        // Both lobbies lead by one away player, so neither is clearly the away team
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow,
            [201, 202, 101], Outcomes.Won, [203, 301], Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID });
        await _context.SaveChangesAsync();

        var details = await _service.GetAsync(match.MatchID);

        Assert.That(details!.Teams.All(t => t.SeasonTeamID == null && t.SeasonTeamName == null), Is.True);
    }

    [Test]
    public async Task GetAsync_LeavesTeamsUnnamed_When_TheSeasonMatchHasNoTeamsYet()
    {
        var (_, seasonMatch) = await GradesTestData.SeedSeason(_context);
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 301, Gamertag = "GT301" }, new XboxUser { XboxUserID = 302, Gamertag = "GT302" });
        await _context.SaveChangesAsync();
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [301], Outcomes.Won, [302], Outcomes.Lost);
        _context.SeriesGames.Add(new SeriesGame { SeasonMatchID = seasonMatch.SeasonMatchID, GameNumber = 1, MatchID = match.MatchID });
        await _context.SaveChangesAsync();

        var details = await _service.GetAsync(match.MatchID);

        Assert.That(details!.Teams.All(t => t.SeasonTeamID == null), Is.True);
    }

    [Test]
    public async Task Controller_Returns404_When_MatchNotPulled()
    {
        var controller = new MatchDetailsController(_service);

        var result = await controller.GetMatchDetails(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Controller_ReturnsTheMatch()
    {
        await SeasonMatchTestData.SeedAsync(_context);
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow, [301], Outcomes.Won, [302], Outcomes.Lost);
        var controller = new MatchDetailsController(_service);

        var result = await controller.GetMatchDetails(match.MatchID, CancellationToken.None);

        Assert.That(result.Value?.MatchID, Is.EqualTo(match.MatchID));
    }
}
