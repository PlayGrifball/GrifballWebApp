using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A Halo Infinite match, its teams, their players and their medals as one aggregate (#332): made by
/// <see cref="Match.FromHalo"/> with its teams, its players added through it, and changed only by its methods (the
/// backfill's variant, the decoder's layout), which refuse the moves no match makes.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloMatchAggregateTests
{
    private static readonly DateTime Start = new(2026, 3, 2, 20, 0, 0, DateTimeKind.Utc);

    private static Match Pulled(Guid? asset = null, Guid? version = null) =>
        Match.FromHalo(Guid.NewGuid(), Start, Start.AddMinutes(10), TimeSpan.FromMinutes(10), Start.AddHours(1), asset, version,
            [new MatchTeamResult(0, 5, Outcomes.Won), new MatchTeamResult(1, 3, Outcomes.Lost)]);

    [Test]
    public void FromHalo_KeepsTheMatchWithItsTeams()
    {
        var (asset, version) = (Guid.NewGuid(), Guid.NewGuid());
        var match = Pulled(asset, version);

        Assert.Multiple(() =>
        {
            Assert.That((match.StartTime, match.EndTime, match.Duration, match.StatsPullDate),
                Is.EqualTo(((DateTime?)Start, (DateTime?)Start.AddMinutes(10), TimeSpan.FromMinutes(10), (DateTime?)Start.AddHours(1))));
            Assert.That((match.UgcGameVariantAssetID, match.UgcGameVariantVersionID), Is.EqualTo(((Guid?)asset, (Guid?)version)));
            Assert.That(match.HasGameVariant, Is.True);
            Assert.That((match.ScoreLayout, match.IsDecoded), Is.EqualTo(((CustomScoreLayout?)null, false)), "Decoded once its players are in");
            Assert.That(match.MatchTeams.Select(t => (t.MatchID, t.TeamID, t.Score, t.Outcome)),
                Is.EqualTo(new[] { (match.MatchID, 0, 5, Outcomes.Won), (match.MatchID, 1, 3, Outcomes.Lost) }));
            Assert.That(match.MatchTeams.Select(t => t.Match), Is.All.SameAs(match));
            Assert.That(match.Participants, Is.Empty);
            Assert.That(Pulled().HasGameVariant, Is.False, "Saved without one: the backfill's to fetch");
        });
    }

    [Test]
    public void FromHalo_RefusesAMatchNoTeamGameMakes()
    {
        Match With(Guid? asset, Guid? version, params MatchTeamResult[] teams) =>
            Match.FromHalo(Guid.NewGuid(), Start, null, TimeSpan.Zero, Start, asset, version, teams);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => With(null, null), "No teams: a free for all isn't kept");
            Assert.Throws<ArgumentException>(() => With(Guid.NewGuid(), null, new MatchTeamResult(0, 1, Outcomes.Won)), "An asset without its version");
            Assert.Throws<ArgumentException>(() => With(null, Guid.NewGuid(), new MatchTeamResult(0, 1, Outcomes.Won)), "A version without its asset");
            Assert.Throws<ArgumentException>(() => With(null, null, new MatchTeamResult(0, 1, Outcomes.Won), new MatchTeamResult(0, 2, Outcomes.Lost)), "A team twice");
            Assert.Throws<ArgumentOutOfRangeException>(() => With(null, null, new MatchTeamResult(0, 1, (Outcomes)0)), "An outcome Halo doesn't give");
        });
    }

    [Test]
    public void AddParticipant_PutsThePlayerOnTheirTeam_WithTheirStatsAndMedals()
    {
        var match = Pulled();
        var xbox = XboxUser.FromXbox(42, "Chief");
        var stats = new MatchParticipantStats
        {
            Score = 3, PersonalScore = 900, Kills = 12, Deaths = 4, Assists = 2, Kda = 10.67f, Accuracy = 51.5f, ShotsFired = 200, ShotsHit = 103,
            AverageLife = TimeSpan.FromSeconds(30), TimePlayed = TimeSpan.FromMinutes(10), FirstJoinedTime = Start, LastLeaveTime = Start.AddMinutes(10),
            PresentAtBeginning = true, PresentAtCompletion = true, Rank = 1, Spawns = 5, ObjectivesCompleted = 3, MaxKillingSpree = 4,
        };

        var player = match.AddParticipant(1, 42, stats, [new MedalCount(77, 2, 50), new MedalCount(78, 1)], xbox);

        Assert.Multiple(() =>
        {
            Assert.That((player.MatchID, player.TeamID, player.XboxUserID), Is.EqualTo((match.MatchID, 1, 42L)));
            Assert.That(player.MatchTeam, Is.SameAs(match.MatchTeams.Single(t => t.TeamID == 1)));
            Assert.That(player.MatchTeam.MatchParticipants, Is.EqualTo(new[] { player }));
            Assert.That(player.XboxUser, Is.SameAs(xbox));
            Assert.That(match.Participants, Is.EqualTo(new[] { player }));
            Assert.That((player.Score, player.PersonalScore, player.Kills, player.Deaths, player.Assists, player.Kda, player.Accuracy, player.ShotsFired, player.ShotsHit),
                Is.EqualTo((3, 900, 12, 4, 2, 10.67f, 51.5f, 200, 103)));
            Assert.That((player.AverageLife, player.TimePlayed, player.FirstJoinedTime, player.LastLeaveTime),
                Is.EqualTo((TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10), Start, (DateTime?)Start.AddMinutes(10))));
            Assert.That((player.PresentAtBeginning, player.PresentAtCompletion, player.JoinedInProgress, player.LeftInProgress), Is.EqualTo((true, true, false, false)));
            Assert.That((player.Rank, player.Spawns, player.ObjectivesCompleted, player.MaxKillingSpree), Is.EqualTo((1, 5, 3, 4)));
            Assert.That(player.Goals, Is.Null, "Not decoded yet");
            Assert.That(player.MedalEarned.Select(m => (m.MatchID, m.XboxUserID, m.MedalID, m.Count, m.TotalPersonalScoreAwarded)),
                Is.EqualTo(new[] { (match.MatchID, 42L, 77L, 2, 50), (match.MatchID, 42L, 78L, 1, 0) }));
            Assert.That(player.MedalEarned.Select(m => m.MatchParticipant), Is.All.SameAs(player));
        });
    }

    [Test]
    public void AddParticipant_RefusesAPlayerNoMatchHas()
    {
        var match = Pulled();
        match.AddParticipant(0, 42, new MatchParticipantStats());

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => match.AddParticipant(2, 43, new MatchParticipantStats()), "A team the match doesn't have");
            Assert.Throws<InvalidOperationException>(() => match.AddParticipant(1, 42, new MatchParticipantStats()), "A player twice, on either team");
            Assert.Throws<ArgumentException>(() => match.AddParticipant(1, 44, new MatchParticipantStats(), [new MedalCount(77, 1), new MedalCount(77, 2)]), "A medal counted twice");
            Assert.Throws<ArgumentException>(() => match.AddParticipant(1, 45, new MatchParticipantStats(), xboxUser: XboxUser.FromXbox(46, "Other")), "Someone else's Xbox user");
            Assert.That(match.Participants.Select(p => p.XboxUserID), Is.EqualTo(new[] { 42L }), "Nothing refused was added");
        });
    }

    [Test]
    public void GiveGameVariant_FillsInAMissingVariant_AndNeverChangesOne()
    {
        var (asset, version) = (Guid.NewGuid(), Guid.NewGuid());
        var match = Pulled();

        match.GiveGameVariant(asset, version);

        Assert.Multiple(() =>
        {
            Assert.That((match.UgcGameVariantAssetID, match.UgcGameVariantVersionID, match.HasGameVariant), Is.EqualTo(((Guid?)asset, (Guid?)version, true)));
            Assert.Throws<InvalidOperationException>(() => match.GiveGameVariant(Guid.NewGuid(), Guid.NewGuid()), "The variant it was played on");
            Assert.Throws<InvalidOperationException>(() => Pulled(asset, version).GiveGameVariant(asset, version));
            Assert.That(match.UgcGameVariantVersionID, Is.EqualTo(version));
        });
    }

    [Test]
    public void Decode_SetsTheLayoutAndEachPlayersStats_AndSaysWhetherAnythingChanged()
    {
        var match = Pulled();
        var scorer = match.AddParticipant(0, 1, new MatchParticipantStats { Score = 201000054, PersonalScore = 201000054 });
        var other = match.AddParticipant(1, 2, new MatchParticipantStats());
        var decoded = new Dictionary<MatchParticipant, DecodedScore>
        {
            [scorer] = new(2, CarrierKills: 1, BallPunches: 0, BallHeldTime: TimeSpan.FromSeconds(108)),
            [other] = DecodedScore.None,
        };

        var first = match.Decode(CustomScoreLayout.B, decoded);
        var again = match.Decode(CustomScoreLayout.B, decoded);
        var relaid = match.Decode(CustomScoreLayout.Undecoded, new Dictionary<MatchParticipant, DecodedScore> { [scorer] = DecodedScore.None, [other] = DecodedScore.None });

        Assert.Multiple(() =>
        {
            Assert.That((first, again, relaid), Is.EqualTo((true, false, true)), "The same result changes nothing");
            Assert.That((match.ScoreLayout, match.IsDecoded), Is.EqualTo(((CustomScoreLayout?)CustomScoreLayout.Undecoded, true)));
            Assert.That((scorer.Goals, scorer.CarrierKills, scorer.BallHeldTime), Is.EqualTo(((int?)null, (int?)null, (TimeSpan?)null)), "Decoded again: nothing kept from before");
            Assert.That((scorer.Score, scorer.PersonalScore), Is.EqualTo((201000054, 201000054)), "The raw values stay");
        });

        match.Decode(CustomScoreLayout.B, decoded);
        Assert.That((scorer.Goals, scorer.CarrierKills, scorer.BallPunches, scorer.BallHeldTime, other.Goals),
            Is.EqualTo(((int?)2, (int?)1, (int?)0, (TimeSpan?)TimeSpan.FromSeconds(108), (int?)null)));
    }

    [Test]
    public void Decode_RefusesAResultThatIsntForEachOfItsPlayers()
    {
        var match = Pulled();
        var player = match.AddParticipant(0, 1, new MatchParticipantStats());
        var stranger = Pulled().AddParticipant(0, 1, new MatchParticipantStats());

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => match.Decode(CustomScoreLayout.Plain, new Dictionary<MatchParticipant, DecodedScore>()), "A player left out");
            Assert.Throws<ArgumentException>(() => match.Decode(CustomScoreLayout.Plain, new Dictionary<MatchParticipant, DecodedScore> { [stranger] = DecodedScore.None }), "Another match's player");
            Assert.Throws<ArgumentException>(() => match.Decode(CustomScoreLayout.Plain, new Dictionary<MatchParticipant, DecodedScore> { [player] = DecodedScore.None, [stranger] = DecodedScore.None }), "One too many");
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Decode((CustomScoreLayout)9, new Dictionary<MatchParticipant, DecodedScore> { [player] = DecodedScore.None }), "A layout the model doesn't know");
            Assert.That(match.IsDecoded, Is.False, "Nothing refused was set");
        });
    }

    [Test]
    public void CountedGoals_AreTheDecodedGoals_OrAnUndecodedScoreOfUpTo20()
    {
        var count = MatchParticipant.CountedGoals.Compile();
        MatchParticipant Player(int score, CustomScoreLayout? layout = null, int? goals = null)
        {
            var match = Pulled();
            var player = match.AddParticipant(0, 1, new MatchParticipantStats { Score = score });
            if (layout is { } decided)
                match.Decode(decided, new Dictionary<MatchParticipant, DecodedScore> { [player] = new(goals) });
            return player;
        }

        Assert.Multiple(() =>
        {
            Assert.That(count(Player(Match.PlainMaxScore)), Is.EqualTo(Match.PlainMaxScore));
            Assert.That(count(Player(Match.PlainMaxScore + 1)), Is.Null, "A packed score isn't goals");
            Assert.That(count(Player(504_010_048, CustomScoreLayout.B, 5)), Is.EqualTo(5));
            Assert.That(count(Player(3, CustomScoreLayout.Undecoded)), Is.Null, "Decoded: only what the layout gives");
        });
    }
}

/// <summary>
/// The database's half of a Halo match's state (#332): it refuses half a game variant, a layout and an outcome the model
/// doesn't know, the children are read only through the matches, and the HaloMatchAggregate migration settles the rows
/// from before, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloMatchAggregateDatabaseTests
{
    private static readonly DateTime Start = new(2026, 3, 2, 20, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static Match Seeded(Guid? asset = null, Guid? version = null, CustomScoreLayout? layout = null)
    {
        var match = Match.Seed(Guid.NewGuid(), startTime: Start, duration: TimeSpan.FromMinutes(10), scoreLayout: layout,
            ugcGameVariantAssetID: asset, ugcGameVariantVersionID: version);
        match.AddTeam(0, 5, Outcomes.Won);
        match.AddTeam(1, 3, Outcomes.Lost);
        return match;
    }

    [Test]
    public async Task The_database_refuses_half_a_variant_and_a_layout_or_outcome_the_model_doesnt_know()
    {
        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(Guid? asset, Guid? version, CustomScoreLayout? layout, Outcomes outcome = Outcomes.Won)
        {
            await using var ctx = _context.NewContextLike();
            var match = Seeded();
            var entry = ctx.Matches.Add(match);
            entry.Property(m => m.UgcGameVariantAssetID).CurrentValue = asset;
            entry.Property(m => m.UgcGameVariantVersionID).CurrentValue = version;
            entry.Property(m => m.ScoreLayout).CurrentValue = layout;
            ctx.SetRaw(match.MatchTeams.First(), t => t.Outcome, outcome);
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(Guid.NewGuid(), null, null), Is.True, "An asset without its version");
            Assert.That(await Refused(null, Guid.NewGuid(), null), Is.True, "A version without its asset");
            Assert.That(await Refused(null, null, (CustomScoreLayout)9), Is.True, "A layout the model doesn't know");
            Assert.That(await Refused(null, null, null, (Outcomes)0), Is.True, "An outcome Halo doesn't give");

            Assert.That(await Refused(null, null, null), Is.False);
            Assert.That(await Refused(Guid.NewGuid(), Guid.NewGuid(), CustomScoreLayout.Undecoded, Outcomes.DidNotFinish), Is.False);
            Assert.That(await Refused(null, null, CustomScoreLayout.Plain, Outcomes.Tie), Is.False);
        });
    }

    [Test]
    public async Task A_match_saves_its_teams_players_and_medals_and_they_are_read_through_the_matches()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(1, "Saved"));
        await GradesTestData.SeedMedals(_context);
        var medal = await _context.Medals.Select(m => m.MedalID).FirstAsync();
        var match = Match.FromHalo(Guid.NewGuid(), Start, Start.AddMinutes(10), TimeSpan.FromMinutes(10), Start, null, null,
            [new MatchTeamResult(0, 2, Outcomes.Won), new MatchTeamResult(1, 1, Outcomes.Lost)]);
        match.AddParticipant(0, 1, new MatchParticipantStats { Score = 2, Kills = 4 }, [new MedalCount(medal, 3)]);
        match.AddParticipant(1, 2, new MatchParticipantStats { Score = 1 }, xboxUser: XboxUser.FromXbox(2, "New"));
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var teams = await _context.HaloMatchTeams.Where(t => t.MatchID == match.MatchID).OrderBy(t => t.TeamID).Select(t => new { t.TeamID, t.Outcome, Players = t.MatchParticipants.Count }).ToListAsync();
        var players = await _context.HaloMatchParticipants.Include(p => p.MedalEarned).Include(p => p.XboxUser)
            .Where(p => p.MatchID == match.MatchID).OrderBy(p => p.XboxUserID).AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(teams.Select(t => (t.TeamID, t.Outcome, t.Players)), Is.EqualTo(new[] { (0, Outcomes.Won, 1), (1, Outcomes.Lost, 1) }));
            Assert.That(players.Select(p => (p.XboxUserID, p.TeamID, p.Score, p.XboxUser.Gamertag)), Is.EqualTo(new[] { (1L, 0, 2, "Saved"), (2L, 1, 1, "New") }));
            Assert.That(players[0].MedalEarned.Select(m => (m.MedalID, m.Count)), Is.EqualTo(new[] { (medal, 3) }));
            Assert.That(players[1].MedalEarned, Is.Empty);
        });
    }

    [Test]
    public async Task The_rules_in_a_query_agree_with_the_loaded_match()
    {
        var (asset, version) = (Guid.NewGuid(), Guid.NewGuid());
        var matches = new[] { Seeded(), Seeded(asset, version), Seeded(layout: CustomScoreLayout.A), Seeded(asset, version, CustomScoreLayout.Ambiguous), Seeded(layout: CustomScoreLayout.Plain) };
        _context.Matches.AddRange(matches);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var withoutVariant = await _context.Matches.Where(Match.WithoutGameVariant).Select(m => m.MatchID).ToListAsync();
        var withVariant = await _context.Matches.Where(Match.WithGameVariant).Select(m => m.MatchID).ToListAsync();
        var withHalosPoints = await _context.Matches.Where(Match.WithHalosPoints).Select(m => m.MatchID).ToListAsync();
        var notDecoded = await _context.Matches.Where(Match.NotDecoded).Select(m => m.MatchID).ToListAsync();
        var loaded = await _context.Matches.AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(withoutVariant, Is.EquivalentTo(new[] { matches[0].MatchID, matches[2].MatchID, matches[4].MatchID }));
            Assert.That(loaded.Where(m => !m.HasGameVariant).Select(m => m.MatchID), Is.EquivalentTo(withoutVariant));
            Assert.That(withVariant, Is.EquivalentTo(new[] { matches[1].MatchID, matches[3].MatchID }));
            Assert.That(loaded.Where(m => m.HasGameVariant).Select(m => m.MatchID), Is.EquivalentTo(withVariant));
            Assert.That(withHalosPoints, Is.EquivalentTo(new[] { matches[0].MatchID, matches[1].MatchID, matches[4].MatchID }), "Not decoded yet, or Plain");
            Assert.That(loaded.Where(m => m.HasHalosPoints).Select(m => m.MatchID), Is.EquivalentTo(withHalosPoints));
            Assert.That(notDecoded, Is.EquivalentTo(new[] { matches[0].MatchID, matches[1].MatchID }));
            Assert.That(loaded.Where(m => !m.IsDecoded).Select(m => m.MatchID), Is.EquivalentTo(notDecoded));
        });
    }

    [Test]
    public async Task The_migration_settles_the_rows_from_before_and_keeps_the_legal_ones()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_HaloMatchAggregate"))) - 1]);

        // Rows as the old model's public setters could leave them
        var (asset, version) = (Guid.NewGuid(), Guid.NewGuid());
        var rows = new Dictionary<string, Match>();
        void Row(string name, Guid? rowAsset, Guid? rowVersion, CustomScoreLayout? layout, Outcomes outcome = Outcomes.Won)
        {
            var match = Seeded();
            var entry = _context.Matches.Add(match);
            entry.Property(m => m.UgcGameVariantAssetID).CurrentValue = rowAsset;
            entry.Property(m => m.UgcGameVariantVersionID).CurrentValue = rowVersion;
            entry.Property(m => m.ScoreLayout).CurrentValue = layout;
            _context.SetRaw(match.MatchTeams.First(), t => t.Outcome, outcome);
            rows[name] = match;
        }
        Row("whole", asset, version, CustomScoreLayout.B);
        Row("no variant", null, null, null);
        Row("an asset alone", asset, null, CustomScoreLayout.Plain);
        Row("a version alone", null, version, null);
        Row("an unknown layout", asset, version, (CustomScoreLayout)9);
        Row("no outcome", null, null, CustomScoreLayout.A, (Outcomes)0);
        Row("an unknown outcome", null, null, null, (Outcomes)7);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.Matches.AsNoTracking().ToDictionaryAsync(m => m.MatchID);
        var outcomes = await _context.Set<MatchTeam>().AsNoTracking().ToDictionaryAsync(t => (t.MatchID, t.TeamID), t => t.Outcome);
        (Guid?, Guid?, CustomScoreLayout?) Of(string name)
        {
            var m = after[rows[name].MatchID];
            return (m.UgcGameVariantAssetID, m.UgcGameVariantVersionID, m.ScoreLayout);
        }
        Outcomes OutcomeOf(string name) => outcomes[(rows[name].MatchID, 0)];
        Assert.Multiple(() =>
        {
            Assert.That(Of("whole"), Is.EqualTo(((Guid?)asset, (Guid?)version, (CustomScoreLayout?)CustomScoreLayout.B)), "Legal: kept");
            Assert.That(Of("no variant"), Is.EqualTo(((Guid?)null, (Guid?)null, (CustomScoreLayout?)null)));
            Assert.That(Of("an asset alone"), Is.EqualTo(((Guid?)null, (Guid?)null, (CustomScoreLayout?)CustomScoreLayout.Plain)), "No variant: the backfill fetches it again");
            Assert.That(Of("a version alone"), Is.EqualTo(((Guid?)null, (Guid?)null, (CustomScoreLayout?)null)));
            Assert.That(Of("an unknown layout"), Is.EqualTo(((Guid?)asset, (Guid?)version, (CustomScoreLayout?)null)), "Undecided: the decoder decides it again");
            Assert.That(OutcomeOf("whole"), Is.EqualTo(Outcomes.Won), "Legal: kept");
            Assert.That(OutcomeOf("no outcome"), Is.EqualTo(Outcomes.DidNotFinish));
            Assert.That(OutcomeOf("an unknown outcome"), Is.EqualTo(Outcomes.DidNotFinish));
            Assert.That(outcomes.Where(o => o.Key.TeamID == 1).Select(o => o.Value), Is.All.EqualTo(Outcomes.Lost), "The other teams as they were");
        });
    }
}
