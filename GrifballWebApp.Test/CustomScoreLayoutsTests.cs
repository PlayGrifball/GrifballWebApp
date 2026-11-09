using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using Match = GrifballWebApp.Domain.Halo.Match;
using Team = Surprenant.Grunt.Models.HaloInfinite.Team;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Test;

/// <summary>
/// Custom-mode scores decoded in the database (#217): a match's game variant kept as it's saved and backfilled for the
/// ones saved before, its layout decided and its players' goals and stats stored, and the stats reading them, on every
/// provider.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CustomScoreLayoutsTests
{
    private GrifballContext _context = null!;
    private long _nextXbox = 1000;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>A saved match: each team its score and its players' (Score, PersonalScore).</summary>
    private async Task<Match> AddMatch(Guid? version, params (int TeamScore, (int Score, int PersonalScore)[] Players)[] teams)
    {
        var id = Guid.NewGuid();
        var match = Match.Seed(id, startTime: DateTime.UtcNow, duration: TimeSpan.FromMinutes(10),
            ugcGameVariantAssetID: version is null ? null : Guid.Parse("11111111-1111-1111-1111-111111111111"), ugcGameVariantVersionID: version);
        foreach (var (t, i) in teams.Select((t, i) => (t, i)))
        {
            match.AddTeam(i, t.TeamScore, i == 0 ? Outcomes.Won : Outcomes.Lost);
            foreach (var p in t.Players)
            {
                var xbox = _nextXbox++;
                match.AddParticipant(i, xbox, new MatchParticipantStats { Score = p.Score, PersonalScore = p.PersonalScore, Kills = 7 },
                    xboxUser: XboxUser.FromXbox(xbox, $"GT{xbox}"));
            }
        }
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return match;
    }

    private Task<List<MatchParticipant>> Players(Guid matchID) =>
        _context.Set<MatchParticipant>().AsNoTracking().Where(p => p.MatchID == matchID).OrderBy(p => p.TeamID).ThenBy(p => p.XboxUserID).ToListAsync();

    private Task<CustomScoreLayout?> LayoutOf(Guid matchID) =>
        _context.Matches.AsNoTracking().Where(m => m.MatchID == matchID).Select(m => m.ScoreLayout).SingleAsync();

    // SML25 Legacy Copy rows 2 and 127 (layout A, 3 and 1 goals), against two players without a goal
    private static readonly (int, (int, int)[])[] AGame = [(4, [(30001124, 30001124), (10203081, 10203081)]), (0, [(2081, 2081), (1004, 1004)])];
    private static readonly (int, (int, int)[])[] NilNil = [(0, [(2081, 2081), (1004, 1004)]), (0, [(41, 41), (0, 0)])];

    [Test]
    public async Task Decode_DecidesTheMatchesNotDecidedYet_AndAVariantsLayoutDecodesIts00Games()
    {
        var version = Guid.NewGuid();
        var aGame = await AddMatch(version, AGame);
        var nilNil = await AddMatch(version, NilNil);
        var unknownNilNil = await AddMatch(null, NilNil);
        var plain = await AddMatch(null, (3, [(1, 2801), (2, 3201)]), (1, [(1, 900), (0, 400)]));
        var none = await AddMatch(null, (5, [(3000000, 15010100), (3000000, 19030200)]), (2, [(2000000, 18020000), (5000000, 53110300)]));

        var changed = await new CustomScoreLayouts(_context).DecodeAsync([]);
        _context.ChangeTracker.Clear();

        var aPlayers = await Players(aGame.MatchID);
        var nilNilPlayers = await Players(nilNil.MatchID);
        Assert.Multiple(async () =>
        {
            Assert.That(changed, Is.EqualTo(5));
            Assert.That(await LayoutOf(aGame.MatchID), Is.EqualTo(CustomScoreLayout.A));
            Assert.That(aPlayers.Select(p => (p.Goals, p.SwordKills, p.PlateStops, p.BallHeldTime)), Is.EqualTo(new[]
            {
                ((int?)3, (int?)1, (int?)0, (TimeSpan?)TimeSpan.FromSeconds(248)),
                (1, 3, 2, TimeSpan.FromSeconds(162)),
                (0, 2, 0, TimeSpan.FromSeconds(162)),
                (0, 1, 0, TimeSpan.FromSeconds(8)),
            }));
            Assert.That(await LayoutOf(nilNil.MatchID), Is.EqualTo(CustomScoreLayout.A), "Its variant's layout, though every layout fits a 0-0");
            Assert.That(nilNilPlayers.Select(p => p.BallHeldTime), Is.All.Not.Null);
            Assert.That(await LayoutOf(unknownNilNil.MatchID), Is.EqualTo(CustomScoreLayout.Ambiguous));
            Assert.That((await Players(unknownNilNil.MatchID)).Select(p => (p.Goals, p.BallHeldTime)), Is.All.EqualTo(((int?)0, (TimeSpan?)null)));
            Assert.That(await LayoutOf(plain.MatchID), Is.EqualTo(CustomScoreLayout.Plain));
            Assert.That((await Players(plain.MatchID)).Select(p => p.Goals), Is.EqualTo(new int?[] { 1, 2, 1, 0 }));
            Assert.That(await LayoutOf(none.MatchID), Is.EqualTo(CustomScoreLayout.Undecoded));
            Assert.That((await Players(none.MatchID)).Select(p => p.Goals), Is.All.Null);
            Assert.That(aPlayers.Select(p => (p.Score, p.PersonalScore, p.Kills)).First(), Is.EqualTo((30001124, 30001124, 7)), "The raw values stay");
        });
    }

    [Test]
    public async Task Decode_IsIdempotent_AndRedecidesAVariantsMatchesWhenAsked()
    {
        var version = Guid.NewGuid();
        var nilNil = await AddMatch(version, NilNil);
        Assert.That(await new CustomScoreLayouts(_context).DecodeAsync([]), Is.EqualTo(1));
        Assert.That(await LayoutOf(nilNil.MatchID), Is.EqualTo(CustomScoreLayout.Ambiguous), "Its variant has no layout yet");

        // The variant's other games come in: asked to look at the variant again, its 0-0 game is read its way
        await AddMatch(version, AGame);
        var progress = new List<(int, int)>();
        var changed = await new CustomScoreLayouts(_context).DecodeAsync([version], (done, of) => progress.Add((done, of)));
        _context.ChangeTracker.Clear();

        Assert.Multiple(async () =>
        {
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(progress, Is.EqualTo(new[] { (2, 2) }));
            Assert.That(await LayoutOf(nilNil.MatchID), Is.EqualTo(CustomScoreLayout.A));
            Assert.That(await new CustomScoreLayouts(_context).DecodeAsync([version]), Is.Zero, "Nothing new: nothing written");
            Assert.That(await new CustomScoreLayouts(_context).DecodeAsync([]), Is.Zero, "Every match is decided");
        });
    }

    private static Player Player(string xuid, int team, int score, int personalScore) => new()
    {
        PlayerId = xuid,
        LastTeamId = team,
        PlayerTeamStats = [new PlayerTeamStat { TeamId = team, Stats = new Stats { CoreStats = new CoreStats { Score = score, PersonalScore = personalScore, Kills = 9, AverageLifeDuration = TimeSpan.FromSeconds(10), Medals = [] } } }],
        ParticipationInfo = new ParticipationInfo { FirstJoinedTime = DateTimeOffset.UtcNow, TimePlayed = TimeSpan.FromMinutes(10) },
    };

    private static MatchStats Stats(Guid id, Guid? asset, Guid? version, params (int Score, Player[] Players)[] teams)
    {
        var info = new MatchInfo
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            EndTime = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMinutes(10),
        };
        if (asset is not null)
            info.UgcGameVariant = new() { AssetId = asset.Value, VersionId = version!.Value };
        return new()
        {
            MatchId = id,
            MatchInfo = info,
            Teams = teams.Select((t, i) => new Team { TeamId = i, Outcome = i == 0 ? 2 : 3, Stats = new Stats { CoreStats = new CoreStats { Score = t.Score, Medals = [] } } }).ToArray(),
            Players = teams.SelectMany(t => t.Players).ToArray(),
        };
    }

    private DataPullService DataPull(IHaloInfiniteClientFactory halo)
    {
        var getsert = Substitute.For<IGetsertXboxUserService>();
        getsert.GetsertXboxUserByXuid(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => XboxUser.FromXbox(ci.Arg<long>(), $"gt{ci.Arg<long>()}"));
        return new DataPullService(Substitute.For<ILogger<DataPullService>>(), halo, _context, getsert);
    }

    [Test]
    public async Task ASavedMatch_KeepsItsVariant_AndIsDecodedAsItsSaved()
    {
        var (id, asset, version) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // SML25 Legacy Copy rows 218 and 219 (layout B, 2 goals each) against row 221
        var stats = Stats(id, asset, version,
            (4, [Player("xuid(1)", 0, 201000048, 201000054), Player("xuid(2)", 0, 208040240, 208040236)]),
            (0, [Player("xuid(3)", 1, 7030041, 7030041)]));

        await DataPull(Substitute.For<IHaloInfiniteClientFactory>()).SaveMatchStats(stats);
        _context.ChangeTracker.Clear();

        var match = await _context.Matches.AsNoTracking().SingleAsync();
        var players = await Players(id);
        Assert.Multiple(() =>
        {
            Assert.That((match.UgcGameVariantAssetID, match.UgcGameVariantVersionID, match.ScoreLayout), Is.EqualTo(((Guid?)asset, (Guid?)version, (CustomScoreLayout?)CustomScoreLayout.B)));
            Assert.That(players.Select(p => (p.Goals, p.CarrierKills, p.BallPunches, p.BallHeldTime)), Is.EqualTo(new[]
            {
                ((int?)2, (int?)1, (int?)0, (TimeSpan?)TimeSpan.FromSeconds(108)),
                (2, 8, 4, TimeSpan.FromSeconds(472)),
                (0, 7, 3, TimeSpan.FromSeconds(82)),
            }));
        });
    }

    [Test]
    public async Task ADecodedMatchWhoseSaveFails_LeavesNothingTracked_AndARetryStoresItDecoded()
    {
        // Decoding happens before the game's single add and save (#222): a failed save lets go of all of it
        var (id, asset, version) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var scorer = Player("xuid(1)", 0, 201000048, 201000054);
        scorer.PlayerTeamStats.Single().Stats.CoreStats.Medals = [new() { NameId = 404, Count = 1 }];
        var stats = Stats(id, asset, version,
            (4, [scorer, Player("xuid(2)", 0, 208040240, 208040236)]),
            (0, [Player("xuid(3)", 1, 7030041, 7030041)]));
        var dataPull = DataPull(Substitute.For<IHaloInfiniteClientFactory>());

        // No medal 404: the save fails on its foreign key
        await Assert.ThrowsAsync<DbUpdateException>(() => dataPull.SaveMatchStats(stats));

        Assert.Multiple(() =>
        {
            Assert.That(_context.ChangeTracker.Entries<Match>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<MatchTeam>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<MatchParticipant>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<MedalEarned>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<XboxUser>(), Is.Empty);
        });

        _context.MedalTypes.Add(MedalType.Imported(1, "t"));
        _context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "d"));
        _context.Medals.Add(GrifballWebApp.Domain.Halo.Medal.Imported(404, "Found", "x", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        await dataPull.SaveMatchStats(stats);
        _context.ChangeTracker.Clear();

        var match = await _context.Matches.AsNoTracking().SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((match.UgcGameVariantVersionID, match.ScoreLayout), Is.EqualTo(((Guid?)version, (CustomScoreLayout?)CustomScoreLayout.B)));
            Assert.That((await Players(id)).Select(p => (p.Goals, p.CarrierKills, p.BallPunches)), Is.EqualTo(new[]
            {
                ((int?)2, (int?)1, (int?)0),
                (2, 8, 4),
                (0, 7, 3),
            }));
        });
    }

    [Test]
    public async Task ASavedMatchWithoutAVariant_IsDecodedByItsTeamScores()
    {
        var id = Guid.NewGuid();
        var stats = Stats(id, null, null, (2, [Player("xuid(1)", 0, 2, 900)]), (1, [Player("xuid(2)", 1, 1, 300)]));

        await DataPull(Substitute.For<IHaloInfiniteClientFactory>()).SaveMatchStats(stats);

        var match = await _context.Matches.AsNoTracking().SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((match.UgcGameVariantVersionID, match.ScoreLayout), Is.EqualTo(((Guid?)null, (CustomScoreLayout?)CustomScoreLayout.Plain)));
            Assert.That((await Players(id)).Select(p => p.Goals), Is.EqualTo(new int?[] { 2, 1 }));
        });
    }

    private static HaloApiResultContainer<MatchStats, HaloApiErrorContainer> Ok(MatchStats stats) => new(stats, null);

    [Test]
    public async Task TheBackfill_SetsOnlyTheVariant_OfMatchesWithoutOne_AndLeavesTheOnesHaloDoesntGiveForLater()
    {
        var (asset, version, otherVersion) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var filled = await AddMatch(null, AGame);
        var failing = await AddMatch(null, AGame);
        var unanswered = await AddMatch(null, AGame);
        var noVariant = await AddMatch(null, AGame);
        var had = await AddMatch(otherVersion, AGame);
        var halo = Substitute.For<IHaloInfiniteClientFactory>();
        // Fetched again, Halo's numbers differ from the saved ones: they're not the backfill's to change
        halo.StatsGetMatchStats(filled.MatchID).Returns(Ok(Stats(filled.MatchID, asset, version, (9, [Player("xuid(1)", 0, 1, 1)]))));
        halo.StatsGetMatchStats(failing.MatchID).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 404, Message = "not found" }));
        halo.StatsGetMatchStats(unanswered.MatchID).Returns(Task.FromException<HaloApiResultContainer<MatchStats, HaloApiErrorContainer>>(new TaskCanceledException("timed out")));
        halo.StatsGetMatchStats(noVariant.MatchID).Returns(Ok(Stats(noVariant.MatchID, null, null, (9, []))));
        var progress = new List<(int, int)>();

        var result = await DataPull(halo).BackfillGameVariants(10, (done, of) => progress.Add((done, of)));
        _context.ChangeTracker.Clear();

        var matches = await _context.Matches.AsNoTracking().ToDictionaryAsync(m => m.MatchID);
        var kept = await _context.Set<MatchTeam>().AsNoTracking().Where(t => t.MatchID == filled.MatchID).OrderBy(t => t.TeamID).Select(t => t.Score).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((result.Missing, result.Attempted, result.Filled, result.NoVariant, result.Failed.Count, result.Left), Is.EqualTo((4, 4, 1, 1, 2, 3)));
            Assert.That(result.Failed, Is.EquivalentTo(new[] { failing.MatchID, unanswered.MatchID }));
            Assert.That(result.Versions, Is.EqualTo(new[] { version }));
            Assert.That(result.Summary, Is.EqualTo("1 of 4 matches given their game variant, 3 left; 2 Halo didn't give (a later run tries them again); 1 without a variant"));
            Assert.That((matches[filled.MatchID].UgcGameVariantAssetID, matches[filled.MatchID].UgcGameVariantVersionID), Is.EqualTo(((Guid?)asset, (Guid?)version)));
            Assert.That(new[] { failing, unanswered, noVariant }.Select(m => matches[m.MatchID].UgcGameVariantVersionID), Is.All.Null);
            Assert.That(matches[had.MatchID].UgcGameVariantVersionID, Is.EqualTo(otherVersion));
            Assert.That(kept, Is.EqualTo(new[] { 4, 0 }), "The saved team scores stay");
            Assert.That((await Players(filled.MatchID)).Select(p => (p.Score, p.Kills)), Is.EqualTo(new[] { (30001124, 7), (10203081, 7), (2081, 7), (1004, 7) }));
            Assert.That(progress, Is.EqualTo(new[] { (4, 4) }));
        });
        await halo.DidNotReceive().StatsGetMatchStats(had.MatchID);

        // Resumed: only the ones still without a variant are asked for, a batch at a time
        halo.ClearReceivedCalls();
        var next = await DataPull(halo).BackfillGameVariants(2);
        Assert.Multiple(async () =>
        {
            Assert.That((next.Missing, next.Attempted), Is.EqualTo((3, 2)));
            await halo.DidNotReceive().StatsGetMatchStats(filled.MatchID);
        });
    }

    [Test]
    public async Task TheBackfill_WithEveryVariantThere_AsksHaloForNothing()
    {
        await AddMatch(Guid.NewGuid(), AGame);
        var halo = Substitute.For<IHaloInfiniteClientFactory>();

        var result = await DataPull(halo).BackfillGameVariants(10);

        Assert.That((result.Missing, result.Attempted, result.Summary), Is.EqualTo((0, 0, "0 of 0 matches given their game variant, 0 left")));
        await halo.DidNotReceiveWithAnyArgs().StatsGetMatchStats(Guid.Empty);
    }

    private MatchVariantBackfillJob Job(IDataPullService dataPull, int? batchSize = null)
    {
        var settings = new Dictionary<string, string?>();
        if (batchSize is not null)
            settings["MatchVariantBackfill:BatchSize"] = batchSize.ToString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new MatchVariantBackfillJob(dataPull, _context, configuration, Substitute.For<ILogger<MatchVariantBackfillJob>>());
    }

    [Test]
    public async Task TheJob_BackfillsABatch_ThenDecodesTheVariantsItFilledIn_AndTheMatchesNotDecodedYet()
    {
        var version = Guid.NewGuid();
        var nilNil = await AddMatch(version, NilNil);
        var aGame = await AddMatch(version, AGame);
        await new CustomScoreLayouts(_context).DecodeAsync([]);
        var undecided = await AddMatch(null, AGame);
        Assert.That(await LayoutOf(nilNil.MatchID), Is.EqualTo(CustomScoreLayout.A));
        // As if the backfill had just given the variant its matches: the 0-0 game is read again
        await _context.Matches.Where(m => m.MatchID == nilNil.MatchID).ExecuteUpdateAsync(s => s.SetProperty(m => m.ScoreLayout, CustomScoreLayout.Ambiguous));
        var dataPull = Substitute.For<IDataPullService>();
        dataPull.BackfillGameVariants(default).ReturnsForAnyArgs(new VariantBackfillResult(5, 3, 2, 0, [Guid.NewGuid()], [version]));
        var context = Substitute.For<IJobExecutionContext>();

        await Job(dataPull, batchSize: 3).Execute(context);

        var report = (JobRunReport)context.Result!;
        Assert.Multiple(async () =>
        {
            await dataPull.Received(1).BackfillGameVariants(3, Arg.Any<Action<int, int>?>(), Arg.Any<CancellationToken>());
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Failed), "Halo didn't give one");
            Assert.That(report.Summary, Is.EqualTo("2 of 3 matches given their game variant, 3 left; 1 Halo didn't give (a later run tries them again); 2 matches decoded again"));
            Assert.That(await LayoutOf(nilNil.MatchID), Is.EqualTo(CustomScoreLayout.A));
            Assert.That(await LayoutOf(undecided.MatchID), Is.EqualTo(CustomScoreLayout.A));
            Assert.That(await LayoutOf(aGame.MatchID), Is.EqualTo(CustomScoreLayout.A));
        });
    }

    [Test]
    public async Task TheJob_WithNothingLeft_IsSkipped_AndUsesTheDefaultBatch()
    {
        var dataPull = Substitute.For<IDataPullService>();
        dataPull.BackfillGameVariants(default).ReturnsForAnyArgs(new VariantBackfillResult(0, 0, 0, 0, []));
        var context = Substitute.For<IJobExecutionContext>();

        await Job(dataPull).Execute(context);

        var report = (JobRunReport)context.Result!;
        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Skipped, "Every match has its game variant and is decoded")));
        await dataPull.Received(1).BackfillGameVariants(MatchVariantBackfillJob.DefaultBatchSize, Arg.Any<Action<int, int>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void TheJob_IsHourly_InTheHaloPullsExecutionGroup()
    {
        var trigger = MatchVariantBackfillJob.Definition.DefaultTrigger(DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(MatchVariantBackfillJob.Key.Name, Is.EqualTo("match-variant-backfill"));
            Assert.That(((ISimpleTrigger)trigger).RepeatInterval, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(typeof(MatchVariantBackfillJob).GetCustomAttributes(typeof(ExecutionGroupAttribute), false), Is.Not.Empty);
        });
    }

    [Test]
    public void TheJobsReport_SucceedsWhenHaloGaveEverything()
    {
        var report = MatchVariantBackfillJob.Report(new VariantBackfillResult(10, 10, 10, 0, []), 0);

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Succeeded, "10 of 10 matches given their game variant, 0 left")));
    }

    [Test]
    public async Task TheStats_CountDecodedGoalsAndStats_AndNoGoalsFromAnUndecidedScoreAbove20()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var encoded = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddMinutes(-40),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost, team0Score: 5, team1Score: 0);
        var undecided = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddMinutes(-20),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.Set<SeriesGame>().AddRange(
            new SeriesGame { MatchID = encoded.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 },
            new SeriesGame { MatchID = undecided.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2 });
        // Layout B: SML25 Legacy rows 218 (2 goals), 219 (2 goals, 8 carrier kills, 4 punches), a 1-goal player and row 221
        var values = new Dictionary<long, int> { [101] = 201000054, [102] = 208040236, [103] = 100000000, [104] = 7030041 };
        // Halo's raw values, as a pull would have saved them
        void Scores(MatchParticipant p, int score, int personalScore)
        {
            _context.Entry(p).Property(x => x.Score).CurrentValue = score;
            _context.Entry(p).Property(x => x.PersonalScore).CurrentValue = personalScore;
        }
        foreach (var p in await _context.Set<MatchParticipant>().Where(p => p.MatchID == encoded.MatchID).ToListAsync())
        {
            var v = values.GetValueOrDefault(p.XboxUserID);
            Scores(p, v, v);
        }
        var big = await _context.Set<MatchParticipant>().SingleAsync(p => p.MatchID == undecided.MatchID && p.XboxUserID == 102);
        Scores(big, 25, 25);
        await _context.SaveChangesAsync();
        await new CustomScoreLayouts(_context).DecodeAsync([]);
        // The other game as one saved before decoding, not decided yet
        await _context.Matches.Where(m => m.MatchID == undecided.MatchID).ExecuteUpdateAsync(s => s.SetProperty(m => m.ScoreLayout, (CustomScoreLayout?)null));
        _context.ChangeTracker.Clear();
        var users = await _context.Users.Where(u => u.XboxUserID == 101 || u.XboxUserID == 102).ToDictionaryAsync(u => u.XboxUserID!.Value, u => u.Id);

        var games = await PlayerGameStatsQuery.Query(_context).Where(g => g.UserID == users[102]).OrderBy(g => g.MatchID == encoded.MatchID ? 0 : 1).ToListAsync();
        var line = (await new SeasonStatsService(_context).GetAsync(userID: users[102])).Single();
        var other = (await new SeasonStatsService(_context).GetAsync(userID: users[101])).Single();
        var grades = (await new GradesService(_context).GetGrades(seeded.Season.SeasonID, CancellationToken.None)).Totals.ToDictionary(t => t.XboxUserID);

        Assert.Multiple(async () =>
        {
            Assert.That(await LayoutOf(encoded.MatchID), Is.EqualTo(CustomScoreLayout.B));
            Assert.That(games.Select(g => (g.Goals, g.CarrierKills, g.Punches, g.SwordKills, g.Score)), Is.EqualTo(new[]
            {
                ((int?)2, (int?)8, (int?)4, (int?)null, (int?)null),
                (null, null, null, null, 25),
            }), "The encoded game's PersonalScore isn't points; the undecided game's 25 isn't goals");
            Assert.That((line.Goals, line.CarrierKills, line.RecordsCarrierKills, line.Punches, line.Score), Is.EqualTo((2L, 8, true, (int?)4, (long?)25)));
            Assert.That(other.Goals, Is.EqualTo(2 + 1), "Its own Score of 1 in the undecided game still counts");
            Assert.That((grades[102].TotalGoals, grades[101].TotalGoals), Is.EqualTo((2, 3)));
        });
    }
}
