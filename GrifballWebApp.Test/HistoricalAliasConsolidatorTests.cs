using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalAliasConsolidatorTests
{
    private GrifballContext _context = null!;
    private IUserMergeService _merges = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _merges = new UserMergeService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static User Placeholder(string gamertag, XboxUser? xbox = null) => new()
    {
        UserName = HistoricalKeys.DummyUserName(gamertag),
        NormalizedUserName = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant(),
        DisplayName = gamertag,
        IsDummyUser = true,
        XboxUser = xbox,
    };

    private async Task Save(params object[] entities)
    {
        _context.AddRange(entities);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private Task<bool> Exists(int userId) => _context.Users.AnyAsync(u => u.Id == userId);

    /// <summary>A merge service whose merges fail inside their transaction (the audit record can't be written).</summary>
    private IUserMergeService FailingMerges()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.GetCurrentUserId().Throws(new InvalidOperationException("boom"));
        return new UserMergeService(_context, currentUser: currentUser);
    }

    [Test]
    public async Task MergesIntoTheCanonicalGamertagsHolder_WhoseHistoryHasAnAlias()
    {
        var xbox = new XboxUser { XboxUserID = 123456789, Gamertag = "MenZ" };
        var holder = new User { UserName = "registered_menz", NormalizedUserName = "REGISTERED_MENZ", DisplayName = "MenZ", XboxUser = xbox };
        var alias = Placeholder("Its Rainin MenZ");
        await Save(holder, alias, new XboxGamertagHistory { XboxUserID = 123456789, Gamertag = "Zerosum33", NormalizedGamertag = "ZEROSUM33", Source = GamertagHistorySource.RenameDetected });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        var merge = await _context.UserMerges.AsNoTracking().SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(result.Merged, Is.EqualTo(new[] { $"merged Its Rainin MenZ into registered_menz (user {holder.Id})" }));
            Assert.That(result.Failed, Is.Empty);
            Assert.That(await Exists(alias.Id), Is.False);
            Assert.That((merge.SourceUserID, merge.TargetUserID, merge.Trigger, merge.Reason), Is.EqualTo((alias.Id, holder.Id, MergeTrigger.Import, "Canonical alias consolidation: Its Rainin MenZ -> MenZ")));
        });
    }

    [Test]
    public async Task TheCanonicalGamertagsHolder_WithoutAnAliasInItsHistory_IsNotTheTarget()
    {
        var unrelated = new User { UserName = "unrelated", NormalizedUserName = "UNRELATED", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "Silva" } };
        var alias = Placeholder("Major Silva");
        await Save(unrelated, alias);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        var made = await _context.Users.AsNoTracking().SingleAsync(u => u.UserName == "historical:Silva");
        Assert.Multiple(async () =>
        {
            Assert.That(result.Merged, Is.EqualTo(new[] { $"merged Major Silva into new placeholder historical:Silva (user {made.Id})" }));
            Assert.That(await Exists(unrelated.Id), Is.True);
            Assert.That(await _context.UserMerges.AnyAsync(m => m.TargetUserID == unrelated.Id), Is.False);
        });
    }

    [Test]
    public async Task TwoHoldersWithAnAliasInTheirHistory_LeaveThePlaceholderAlone()
    {
        var a = new User { UserName = "a", NormalizedUserName = "A", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "MenZ" } };
        var b = new User { UserName = "b", NormalizedUserName = "B", XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "MenZ" } };
        var alias = Placeholder("Its Rainin MenZ");
        await Save(a, b, alias,
            new XboxGamertagHistory { XboxUserID = 1, Gamertag = "Zerosum33", NormalizedGamertag = "ZEROSUM33", Source = GamertagHistorySource.Manual },
            new XboxGamertagHistory { XboxUserID = 2, Gamertag = "I CITI I", NormalizedGamertag = "I CITI I", Source = GamertagHistorySource.Manual });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await Exists(alias.Id), Is.True);
        Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
    }

    [Test]
    public async Task MergesIntoTheCanonicalPlaceholder()
    {
        var canonical = Placeholder("MenZ");
        var alias = Placeholder("MenztrualCramps");
        await Save(canonical, alias);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.EqualTo(new[] { $"merged MenztrualCramps into historical:MenZ (user {canonical.Id})" }));
        Assert.That(await _context.Users.Select(u => u.Id).ToListAsync(), Is.EqualTo(new[] { canonical.Id }));
    }

    [Test]
    public async Task WithNoCanonicalAccount_MakesItsPlaceholder_AndMergesIntoIt_AsAnAuditedMerge()
    {
        var alias = Placeholder("Major Silva", new XboxUser { XboxUserID = 55, Gamertag = "Major Silva" });
        await Save(alias);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        var made = await _context.Users.AsNoTracking().SingleAsync();
        var merge = await _context.UserMerges.AsNoTracking().SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Merged, Has.Count.EqualTo(1));
            Assert.That((made.UserName, made.DisplayName, made.IsDummyUser, made.XboxUserID), Is.EqualTo(("historical:Silva", "Silva", true, (long?)55)), "The Xbox account moves with it");
            Assert.That((merge.SourceUserID, merge.SourceUserName, merge.TargetUserID), Is.EqualTo((alias.Id, "historical:Major Silva", made.Id)), "A sysadmin can see and undo it");
        });
    }

    [Test]
    public async Task OnlyAliasPlaceholdersAreTouched()
    {
        var other = Placeholder("Someone Else");
        var xuidNamed = new User { UserName = "historical:Its Rainin MenZ:2533274900000001", NormalizedUserName = "HISTORICAL:ITS RAININ MENZ:2533274900000001", DisplayName = "Its Rainin MenZ", IsDummyUser = true };
        var canonical = Placeholder("MenZ");
        await Save(other, xuidNamed, canonical);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await _context.Users.CountAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task SkipsAMerge_WhenBothAccountsHoldXboxLinks()
    {
        var canonical = Placeholder("MenZ", new XboxUser { XboxUserID = 101, Gamertag = "MenZ" });
        var alias = Placeholder("Its Rainin MenZ", new XboxUser { XboxUserID = 202, Gamertag = "Its Rainin MenZ" });
        // The canonical one is the player (its history has an alias): only both holding Xbox accounts stops it
        await Save(canonical, alias, new XboxGamertagHistory { XboxUserID = 101, Gamertag = "Zerosum33", NormalizedGamertag = "ZEROSUM33", Source = GamertagHistorySource.Manual });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await _context.Users.Where(u => u.Id == canonical.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(101));
        Assert.That(await _context.Users.Where(u => u.Id == alias.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(202));
    }

    [Test]
    public async Task KeepsTheTargetsXbox_WhenOnlyTheTargetHasOne_AndItsHistoryHasAnAlias()
    {
        // The canonical placeholder is tied to an Xbox account called something else now, which was Its Rainin MenZ
        var canonical = Placeholder("MenZ", new XboxUser { XboxUserID = 303, Gamertag = "Renamed Since" });
        var alias = Placeholder("Its Rainin MenZ");
        await Save(canonical, alias, new XboxGamertagHistory { XboxUserID = 303, Gamertag = "Its Rainin MenZ", NormalizedGamertag = "ITS RAININ MENZ", Source = GamertagHistorySource.RenameDetected });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Has.Count.EqualTo(1));
        Assert.That(await _context.Users.Where(u => u.Id == canonical.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(303));
        Assert.That(await Exists(alias.Id), Is.False);
    }

    [Test]
    public async Task TheAliasPlaceholderThatHoldsTheCanonicalGamertag_IsLeft()
    {
        // Its Xbox account is called MenZ now and was Zerosum33: it is the player
        var alias = Placeholder("Its Rainin MenZ", new XboxUser { XboxUserID = 404, Gamertag = "MenZ" });
        await Save(alias, new XboxGamertagHistory { XboxUserID = 404, Gamertag = "Zerosum33", NormalizedGamertag = "ZEROSUM33", Source = GamertagHistorySource.Manual });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await _context.Users.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SkipsAMerge_TheMergeRulesRefuse()
    {
        var canonical = Placeholder("MenZ");
        var alias = Placeholder("MenztrualCramps");
        await Save(canonical, alias);
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(canonical.Id, alias.Id, Arg.Any<CancellationToken>()).Returns("Both users have historical stats for season X");

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.That(result.Merged, Is.Empty);
        await merges.DidNotReceiveWithAnyArgs().Merge(default, default);
    }

    [Test]
    public async Task AMergeASysadminUndid_IsNotMadeAgain()
    {
        var canonical = Placeholder("MenZ");
        var alias = Placeholder("MenztrualCramps");
        await Save(canonical, alias);
        await _merges.Merge(canonical.Id, alias.Id, MergeOptions.For(MergeTrigger.Import, "Canonical alias consolidation: MenztrualCramps -> MenZ"));
        var mergeId = await _context.UserMerges.Select(m => m.UserMergeID).SingleAsync();
        _context.ChangeTracker.Clear();
        await new UserMergeAuditService(_context, TimeProvider.System).Undo(mergeId, "not the same player");
        _context.ChangeTracker.Clear();
        Assert.That(await Exists(alias.Id), Is.True, "The undo restored it");

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await Exists(alias.Id), Is.True);
        Assert.That(await _context.UserMerges.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task AFailedMerge_IsRolledBackAndReported_AndTheNextOnesStillCommit()
    {
        var canonical = Placeholder("MenZ");
        var first = Placeholder("MenztrualCramps");
        var second = Placeholder("Its Rainin MenZ");
        await Save(canonical, first, second);
        // The first merge fails (and, like Merge, leaves no transaction open); the next is a real one
        var real = new UserMergeService(_context);
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        merges.Merge(canonical.Id, first.Id, Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("constraint clash")));
        merges.Merge(canonical.Id, second.Id, Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(c => real.Merge(c.ArgAt<int>(0), c.ArgAt<int>(1), c.ArgAt<MergeOptions?>(2), c.ArgAt<CancellationToken>(3)));

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Failed, Is.EqualTo(new[] { "MenztrualCramps into MenZ: constraint clash" }));
            Assert.That(result.Merged, Has.Count.EqualTo(1));
            Assert.That(_context.InTransaction, Is.False, "Nothing left open for the job's saves after it");
            Assert.That(await Exists(first.Id), Is.True);
            Assert.That(await Exists(second.Id), Is.False);
            Assert.That(await _context.UserMerges.Select(m => m.SourceUserID).SingleAsync(), Is.EqualTo(second.Id));
        });
    }

    [Test]
    public async Task AFailedMergeIntoANewPlaceholder_LeavesNoPlaceholderBehind()
    {
        var alias = Placeholder("Major Silva");
        await Save(alias);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, FailingMerges());

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(result.Failed, Is.EqualTo(new[] { "Major Silva into Silva: boom" }));
            Assert.That(_context.InTransaction, Is.False);
            Assert.That(await _context.Users.Select(u => u.UserName).ToListAsync(), Is.EqualTo(new[] { "historical:Major Silva" }));
            Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task InACallersTransaction_AFailedMergeStopsTheRun()
    {
        var canonical = Placeholder("MenZ");
        var first = Placeholder("MenztrualCramps");
        var second = Placeholder("Its Rainin MenZ");
        await Save(canonical, first, second);
        await _context.StartTransactionAsync();

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, FailingMerges());

        Assert.That(result.Failed, Has.Count.EqualTo(2));
        Assert.That(result.Failed[1], Does.StartWith("Stopped"));
        Assert.That(_context.InTransaction, Is.True, "The caller's, for it to roll back");
        await Assert.ThrowsAsync<Exception>(() => _context.CommitTransactionAsync());
        await _context.RollbackTransactionAsync();
        Assert.That(_context.InTransaction, Is.False);
    }

    [Test]
    public async Task ACancelledRun_IsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        merges.Merge(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await Save(Placeholder("MenZ"), Placeholder("MenztrualCramps"));
            await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges, ct: cts.Token);
        });
    }

    // The merge and the context's transactions, which a failed consolidation relies on

    [Test]
    public async Task AMergeThatFailsInsideItsTransaction_RollsBack_AndLeavesNoneOpen()
    {
        var target = Placeholder("MenZ");
        var source = Placeholder("MenztrualCramps");
        await Save(target, source);

        await Assert.ThrowsAsync<InvalidOperationException>(() => FailingMerges().Merge(target.Id, source.Id, MergeOptions.For(MergeTrigger.Import)));

        _context.ChangeTracker.Clear();
        Assert.That(_context.InTransaction, Is.False);
        Assert.That(await Exists(source.Id), Is.True, "Its deletion was rolled back");
        Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
        // And the context can merge again
        await _merges.Merge(target.Id, source.Id, MergeOptions.For(MergeTrigger.Import));
        Assert.That(await Exists(source.Id), Is.False);
    }

    [Test]
    public async Task ARolledBackTransaction_LetsTheNextOneCommit()
    {
        await _context.StartTransactionAsync();
        _context.Users.Add(Placeholder("Rolled Back"));
        await _context.SaveChangesAsync();
        await _context.RollbackTransactionAsync();
        _context.ChangeTracker.Clear();

        await _context.StartTransactionAsync();
        _context.Users.Add(Placeholder("Committed"));
        await _context.SaveChangesAsync();
        await _context.CommitTransactionAsync();

        Assert.That(_context.InTransaction, Is.False);
        Assert.That(await _context.Users.Select(u => u.DisplayName).ToListAsync(), Is.EqualTo(new[] { "Committed" }));
    }

    [Test]
    public async Task AnInnerRollback_LeavesTheOuterOnlyARollback()
    {
        await _context.StartTransactionAsync();
        await _context.StartTransactionAsync();
        await _context.RollbackTransactionAsync();

        Assert.That(_context.InTransaction, Is.True);
        await Assert.ThrowsAsync<Exception>(() => _context.CommitTransactionAsync());
        await _context.RollbackTransactionAsync();
        Assert.That(_context.InTransaction, Is.False);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.RollbackTransactionAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.CommitTransactionAsync());
    }

    [Test]
    public async Task APlaceholderWithAnUndoneConsolidation_IsLeft()
    {
        var alias = Placeholder("Major Silva");
        await Save(alias);
        await Save(new UserMerge { MergedAt = DateTime.UtcNow, Trigger = MergeTrigger.Import, Reason = "Canonical alias consolidation: Major Silva -> Silva", SourceUserID = alias.Id, SourceUserName = alias.UserName, TargetUserID = 9999, UndoneAt = DateTime.UtcNow });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await _context.Users.Select(u => u.Id).ToListAsync(), Is.EqualTo(new[] { alias.Id }), "No canonical placeholder made for it either");
    }

    [Test]
    public async Task AnotherUndoneMergeOfThePlaceholder_DoesNotStopItsConsolidation()
    {
        // A sysadmin merged it into the wrong user by hand, and undid that; an import merge for another reason, undone too;
        // and a consolidation into a canonical player the registry no longer maps it to
        var alias = Placeholder("Major Silva");
        await Save(alias);
        await Save(
            new UserMerge { MergedAt = DateTime.UtcNow, Trigger = MergeTrigger.Manual, Reason = "Canonical alias consolidation: Major Silva -> Silva", SourceUserID = alias.Id, SourceUserName = alias.UserName, TargetUserID = 9999, UndoneAt = DateTime.UtcNow },
            new UserMerge { MergedAt = DateTime.UtcNow, Trigger = MergeTrigger.Import, Reason = "claim 12", SourceUserID = alias.Id, SourceUserName = alias.UserName, TargetUserID = 9998, UndoneAt = DateTime.UtcNow },
            new UserMerge { MergedAt = DateTime.UtcNow, Trigger = MergeTrigger.Import, Reason = "Canonical alias consolidation: Major Silva -> Someone Else", SourceUserID = alias.Id, SourceUserName = alias.UserName, TargetUserID = 9997, UndoneAt = DateTime.UtcNow });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Has.Count.EqualTo(1));
        Assert.That(result.Merged[0], Does.StartWith("merged Major Silva into new placeholder historical:Silva"));
        Assert.That(await Exists(alias.Id), Is.False);
    }

    [Test]
    public async Task WithOnlyANumberedCanonicalPlaceholder_MergesIntoIt()
    {
        var numbered = Placeholder("Silva#2");
        var alias = Placeholder("Major Silva");
        await Save(numbered, alias);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.EqualTo(new[] { $"merged Major Silva into historical:Silva#2 (user {numbered.Id})" }));
        Assert.That(await _context.Users.Select(u => u.Id).ToListAsync(), Is.EqualTo(new[] { numbered.Id }));
    }

    [Test]
    public async Task ACanonicalPlaceholderTiedToAnXboxAccountWithoutAnAlias_IsNotTheTarget()
    {
        // An unrelated Waypoint opponent's Xbox account is called Silva: its placeholder is not Major Silva
        var opponent = Placeholder("Silva", new XboxUser { XboxUserID = 66, Gamertag = "Silva" });
        var alias = Placeholder("Major Silva");
        await Save(opponent, alias);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Merged, Is.Empty);
            Assert.That(result.Failed, Is.Empty);
            Assert.That(await _context.Users.Select(u => u.Id).OrderBy(id => id).ToListAsync(), Is.EqualTo(new[] { opponent.Id, alias.Id }), "Nothing merged, nothing made");
            Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task TheCanonicalGamertagsHolder_IsFoundByTheNormalisedGamertag()
    {
        // Stored with a double space: the import's matching finds it, so the consolidation does too
        var holder = new User { UserName = "scet", NormalizedUserName = "SCET", XboxUser = new XboxUser { XboxUserID = 77, Gamertag = "Scet  Shin" } };
        var alias = Placeholder("oScet");
        await Save(holder, alias, new XboxGamertagHistory { XboxUserID = 77, Gamertag = "oScet", NormalizedGamertag = "OSCET", Source = GamertagHistorySource.RenameDetected });

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.EqualTo(new[] { $"merged oScet into scet (user {holder.Id})" }));
        Assert.That(await Exists(alias.Id), Is.False);
    }

    // The import's decision that two gamertags of one handle are two players (PlayerResolver.SharedAliases)

    private async Task<Season> NewSeason(string name)
    {
        var season = new Season
        {
            SeasonName = name,
            GameVersionID = 1,
            HistoricalSourceID = name,
            SeasonStart = new DateTime(2012, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await Save(season);
        return season;
    }

    private static PlayerSeasonStat Row(User user, Season season, string gamertag, bool playoffs = false)
        => new() { UserID = user.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = gamertag, GamesPlayed = 1, IsPlayoffs = playoffs };

    private async Task<Team> NewTeam(Season season)
    {
        var team = new Team { TeamName = "Scet's Team", SeasonID = season.SeasonID };
        await Save(team);
        return team;
    }

    private static PlayerChampionship Title(User user, Team team, string gamertag, string tier = "Pro")
        => new() { UserID = user.Id, SeasonID = team.SeasonID, GameVersionID = 1, Tier = tier, TeamID = team.TeamID, GamertagUsed = gamertag };

    private static IUserMergeService MergesThatAllowAll()
    {
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        return merges;
    }

    [Test]
    public async Task APlaceholderKeptApartInASheetsPhase_IsLeft()
    {
        var canonical = Placeholder("Scet Shin");
        var alias = Placeholder("Scet");
        await Save(canonical, alias);
        var season = await NewSeason("Winter League 2012");
        await Save(Row(canonical, season, "Scet Shin"), Row(alias, season, "Scet"));
        var merges = MergesThatAllowAll();

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.That(result.Merged, Is.Empty);
        await merges.DidNotReceiveWithAnyArgs().ConflictAsync(default, default);
        await merges.DidNotReceiveWithAnyArgs().Merge(default, default);
    }

    [Test]
    public async Task APlaceholderKeptApartOnATitleRoster_IsLeft_WithBothTitles()
    {
        // A champions workbook with no stat sheet: one roster, one team, nothing the merge rules refuse
        var canonical = Placeholder("Scet Shin");
        var alias = Placeholder("Scet");
        await Save(canonical, alias);
        var team = await NewTeam(await NewSeason("Spring League 2012"));
        await Save(Title(canonical, team, "Scet Shin"), Title(alias, team, "Scet"));

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Merged, Is.Empty);
            Assert.That(await Exists(alias.Id), Is.True);
            Assert.That(await _context.PlayerChampionships.CountAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task APlaceholderKeptApartInALateLeagueSeason_IsLeft()
    {
        var canonical = Placeholder("Scet Shin");
        var alias = Placeholder("Scet");
        await Save(canonical, alias);
        var late = new MatchmakingSeason { Name = "Late League Season 8 (Halo 3)", StartsAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndsAt = new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        await Save(late);
        await Save(
            new MatchmakingPlayerStat { UserID = canonical.Id, MatchmakingSeasonID = late.MatchmakingSeasonID, GameVersionID = 1, Gamertag = "Scet Shin", Kills = 4, GamesPlayed = 1 },
            new MatchmakingPlayerStat { UserID = alias.Id, MatchmakingSeasonID = late.MatchmakingSeasonID, GameVersionID = 1, Gamertag = "Scet", Kills = 2, GamesPlayed = 1 });
        var merges = MergesThatAllowAll();

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.That(result.Merged, Is.Empty);
        await merges.DidNotReceiveWithAnyArgs().ConflictAsync(default, default);
    }

    [Test]
    public async Task APlaceholderKeptApartInARecordedGame_IsLeft()
    {
        var canonical = Placeholder("Scet Shin");
        var alias = Placeholder("Scet");
        await Save(canonical, alias);
        var game = new RecordedGame { SourceKey = "mcc/scet", GameVersionID = 1 };
        game.Participants.Add(new RecordedGameParticipant { UserID = canonical.Id, Gamertag = "Scet Shin", Kills = 3 });
        game.Participants.Add(new RecordedGameParticipant { UserID = alias.Id, Gamertag = "Scet", Kills = 1 });
        await Save(game);

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, _merges);

        Assert.That(result.Merged, Is.Empty);
        Assert.That(await Exists(alias.Id), Is.True);
    }

    [Test]
    public async Task APlaceholderInOtherListsThanTheHandlesOtherGamertags_IsMerged()
    {
        // Each list has one gamertag of the handle: a phase, a tier and a game of their own, or the same gamertag
        var canonical = Placeholder("Scet Shin");
        var alias = Placeholder("Scet");
        var other = Placeholder("Someone Else");
        await Save(canonical, alias, other);
        var season = await NewSeason("Winter League 2012");
        var team = await NewTeam(season);
        await Save(
            Row(alias, season, "Scet"), Row(canonical, season, "Scet Shin", playoffs: true), Row(other, season, "Someone Else"),
            Title(alias, team, "Scet"), Title(canonical, team, "Scet Shin", tier: "Amateur"), Title(other, team, "Scet"));
        var late = new MatchmakingSeason { Name = "Late League Season 9 (Halo 3)", StartsAt = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndsAt = new DateTime(2021, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        await Save(late);
        await Save(new MatchmakingPlayerStat { UserID = alias.Id, MatchmakingSeasonID = late.MatchmakingSeasonID, GameVersionID = 1, Gamertag = "Scet", Kills = 2, GamesPlayed = 1 },
            new MatchmakingPlayerStat { UserID = other.Id, MatchmakingSeasonID = late.MatchmakingSeasonID, GameVersionID = 1, Gamertag = "Someone Else", Kills = 2, GamesPlayed = 1 });
        var game = new RecordedGame { SourceKey = "mcc/scet-2", GameVersionID = 1 };
        game.Participants.Add(new RecordedGameParticipant { UserID = alias.Id, Gamertag = "Scet", Kills = 1 });
        game.Participants.Add(new RecordedGameParticipant { UserID = other.Id, Gamertag = "Someone Else", Kills = 1 });
        await Save(game);
        var merges = MergesThatAllowAll();

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.That(result.Merged, Is.EqualTo(new[] { $"merged Scet into historical:Scet Shin (user {canonical.Id})" }));
        await merges.Received(1).Merge(canonical.Id, alias.Id, Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InACallersTransaction_TheMergesARollbackUndid_AreReportedAsRolledBack()
    {
        var canonical = Placeholder("MenZ");
        var first = Placeholder("MenztrualCramps");
        var second = Placeholder("Its Rainin MenZ");
        await Save(canonical, first, second);
        // The first merge is a real one; the second fails inside its transaction, which takes the caller's down with it
        var real = new UserMergeService(_context);
        var failing = FailingMerges();
        var merges = MergesThatAllowAll();
        merges.Merge(canonical.Id, first.Id, Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(c => real.Merge(c.ArgAt<int>(0), c.ArgAt<int>(1), c.ArgAt<MergeOptions?>(2), c.ArgAt<CancellationToken>(3)));
        merges.Merge(canonical.Id, second.Id, Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>())
            .Returns(c => failing.Merge(c.ArgAt<int>(0), c.ArgAt<int>(1), c.ArgAt<MergeOptions?>(2), c.ArgAt<CancellationToken>(3)));
        await _context.StartTransactionAsync();

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.Multiple(() =>
        {
            Assert.That(result.Merged, Is.Empty, "Undone by the rollback");
            Assert.That(result.Failed, Is.EqualTo(new[]
            {
                "Its Rainin MenZ into MenZ: boom",
                $"Rolled back: merged MenztrualCramps into historical:MenZ (user {canonical.Id})",
                "Stopped: the transaction it ran in was rolled back",
            }));
            Assert.That(_context.RolledBackInside, Is.True);
        });
        await _context.RollbackTransactionAsync();
        _context.ChangeTracker.Clear();
        Assert.That(await Exists(first.Id), Is.True, "Its merge was rolled back with the caller's transaction");
    }

    [Test]
    public async Task InACallersTransaction_AFailureThatRolledNothingBack_KeepsTheMergesMade()
    {
        var canonical = Placeholder("MenZ");
        var first = Placeholder("MenztrualCramps");
        var second = Placeholder("Its Rainin MenZ");
        await Save(canonical, first, second);
        var merges = Substitute.For<IUserMergeService>();
        merges.ConflictAsync(canonical.Id, first.Id, Arg.Any<CancellationToken>()).Returns((string?)null);
        merges.ConflictAsync(canonical.Id, second.Id, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("query failed"));
        await _context.StartTransactionAsync();

        var result = await HistoricalAliasConsolidator.ConsolidateAsync(_context, merges);

        Assert.That(result.Merged, Has.Count.EqualTo(1));
        Assert.That(result.Failed, Is.EqualTo(new[] { "Its Rainin MenZ into MenZ: query failed", "Stopped: it runs in the caller's transaction, which decides what to do with the failure" }));
        Assert.That(_context.RolledBackInside, Is.False);
        await _context.CommitTransactionAsync();
    }

    [Test]
    public async Task AMergeWhoseTransactionTheServerEnded_ThrowsItsOwnError()
    {
        var target = Placeholder("MenZ");
        var source = Placeholder("MenztrualCramps");
        await Save(target, source);
        // A deadlock victim: the server rolled the transaction back, the merge fails with its own error
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.GetCurrentUserId().Returns(_ =>
        {
            _context.Database.CurrentTransaction!.GetDbTransaction().Rollback();
            throw new InvalidOperationException("deadlock victim");
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new UserMergeService(_context, currentUser: currentUser).Merge(target.Id, source.Id, MergeOptions.For(MergeTrigger.Import)));

        Assert.That(error!.Message, Is.EqualTo("deadlock victim"));
        Assert.That(_context.InTransaction, Is.False);
        _context.ChangeTracker.Clear();
        Assert.That(await Exists(source.Id), Is.True);
    }

    /// <summary>A context whose rollback fails (after rolling back), as a broken connection's can.</summary>
    private sealed class RollbackFailsContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        public override async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            await base.RollbackTransactionAsync(cancellationToken);
            throw new InvalidOperationException("This SqlTransaction has completed; it is no longer usable.");
        }
    }

    [Test]
    public async Task AMergeWhoseRollbackFails_ThrowsItsOwnError()
    {
        var target = Placeholder("MenZ");
        var source = Placeholder("MenztrualCramps");
        await Save(target, source);
        await using var context = new RollbackFailsContext(TestDatabase.Options(_context.Database.GetConnectionString()!).Options);
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.GetCurrentUserId().Throws(new InvalidOperationException("boom"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new UserMergeService(context, currentUser: currentUser).Merge(target.Id, source.Id, MergeOptions.For(MergeTrigger.Import)));

        Assert.That(error!.Message, Is.EqualTo("boom"), "The merge's error, not the rollback's");
        Assert.That(context.InTransaction, Is.False);
        Assert.That(await Exists(source.Id), Is.True);
    }
}
