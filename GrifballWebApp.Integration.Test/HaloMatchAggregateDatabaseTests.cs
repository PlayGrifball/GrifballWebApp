using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(Guid.NewGuid(), null, null), Is.True, "An asset without its version");
            Assert.That(await Refused(null, Guid.NewGuid(), null), Is.True, "A version without its asset");
            Assert.That(await Refused(null, null, (CustomScoreLayout)9), Is.True, "A layout the model doesn't know");
            Assert.That(await Refused(null, null, null, (Outcomes)0), Is.True, "An outcome Halo doesn't give");

            Assert.That(await Refused(null, null, null), Is.False);
            Assert.That(await Refused(Guid.NewGuid(), Guid.NewGuid(), CustomScoreLayout.Undecoded, Outcomes.DidNotFinish), Is.False);
            Assert.That(await Refused(null, null, CustomScoreLayout.Plain, Outcomes.Tie), Is.False);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Select(t => (t.TeamID, t.Outcome, t.Players)), Is.EqualTo(new[] { (0, Outcomes.Won, 1), (1, Outcomes.Lost, 1) }));
            Assert.That(players.Select(p => (p.XboxUserID, p.TeamID, p.Score, p.XboxUser.Gamertag)), Is.EqualTo(new[] { (1L, 0, 2, "Saved"), (2L, 1, 1, "New") }));
            Assert.That(players[0].MedalEarned.Select(m => (m.MedalID, m.Count)), Is.EqualTo(new[] { (medal, 3) }));
            Assert.That(players[1].MedalEarned, Is.Empty);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutVariant, Is.EquivalentTo(new[] { matches[0].MatchID, matches[2].MatchID, matches[4].MatchID }));
            Assert.That(loaded.Where(m => !m.HasGameVariant).Select(m => m.MatchID), Is.EquivalentTo(withoutVariant));
            Assert.That(withVariant, Is.EquivalentTo(new[] { matches[1].MatchID, matches[3].MatchID }));
            Assert.That(loaded.Where(m => m.HasGameVariant).Select(m => m.MatchID), Is.EquivalentTo(withVariant));
            Assert.That(withHalosPoints, Is.EquivalentTo(new[] { matches[0].MatchID, matches[1].MatchID, matches[4].MatchID }), "Not decoded yet, or Plain");
            Assert.That(loaded.Where(m => m.HasHalosPoints).Select(m => m.MatchID), Is.EquivalentTo(withHalosPoints));
            Assert.That(notDecoded, Is.EquivalentTo(new[] { matches[0].MatchID, matches[1].MatchID }));
            Assert.That(loaded.Where(m => !m.IsDecoded).Select(m => m.MatchID), Is.EquivalentTo(notDecoded));
        }
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
        using (Assert.EnterMultipleScope())
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
        }
    }
}
