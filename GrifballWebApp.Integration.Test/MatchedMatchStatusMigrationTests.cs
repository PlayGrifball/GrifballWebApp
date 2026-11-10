using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The MatchedMatchStatus migration fills Status from Active before dropping it: a match with a result on record is
/// reported, one still active is active, and any other ended one canceled; in the history too, alike on whichever
/// database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchedMatchStatusMigrationTests
{
    private GrifballContext _context;

    /// <summary>The column the migration replaces: no member of the model now, so a shadow property of the old one.</summary>
    private const string ActiveColumn = "Active";

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Status_IsFilledFromActive_AndTheResult_InTheTableAndItsHistory()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_MatchedMatchStatus"))) - 1]);

        var halo = Match.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), duration: TimeSpan.FromMinutes(10));
        var halo2 = Match.Seed(Guid.NewGuid(), startTime: halo.StartTime, duration: halo.Duration);
        var season = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.AddRange(halo, halo2, season);
        await _context.SaveChangesAsync();

        // Through the model as it was then: Active, and no Status or WinnerSide
        int active, ended, reported, reportedStillFlagged, archived;
        await using (var before = new ContextBeforeStatus(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).Options))
        {
            MatchedMatch Old(MatchedMatch match, bool isActive)
            {
                before.MatchedMatches.Add(match);
                before.Entry(match).Property(ActiveColumn).CurrentValue = isActive;
                return match;
            }
            var playing = Old(MatchedMatch.Start([], []), true);
            var over = Old(MatchedMatch.Start([], []), false);
            var withHalo = Old(MatchedMatch.Start([], []).Reported(halo.MatchID), false);
            // Reported, yet still flagged active: an old row could be both
            var flagged = Old(MatchedMatch.Start([], []).Reported(halo2.MatchID), true);
            var archive = Old(MatchedMatch.Archive(RecordedGame.Seed("archive/late/1"), season.MatchmakingSeasonID, season.StartsAt), false);
            await before.SaveChangesAsync();
            // An update before the migration, for a row in the history: the active match's thread
            playing.AttachThread(5);
            await before.SaveChangesAsync();
            (active, ended, reported, reportedStillFlagged, archived) = (playing.Id, over.Id, withHalo.Id, flagged.Id, archive.Id);
        }

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var statuses = await _context.MatchedMatches.ToDictionaryAsync(m => m.Id, m => (m.Status, m.WinnerSide));
        var history = await _context.Database.SqlQueryRaw<int?>(TestDatabase.Provider == DatabaseProvider.Sqlite
            ? "SELECT \"Status\" AS \"Value\" FROM \"MatchedMatchesHistory\""
            : "SELECT \"Status\" AS \"Value\" FROM \"Matchmaking\".\"MatchedMatchesHistory\"").ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(statuses[active], Is.EqualTo((MatchedMatchStatus.Active, (MatchedSide?)null)));
            Assert.That(statuses[ended], Is.EqualTo((MatchedMatchStatus.Canceled, (MatchedSide?)null)), "Vote-ended and canceled were stored alike");
            Assert.That(statuses[reported], Is.EqualTo((MatchedMatchStatus.Reported, (MatchedSide?)null)));
            Assert.That(statuses[reportedStillFlagged], Is.EqualTo((MatchedMatchStatus.Reported, (MatchedSide?)null)), "A result wins over Active");
            Assert.That(statuses[archived], Is.EqualTo((MatchedMatchStatus.Reported, (MatchedSide?)null)));
            Assert.That(history, Does.Contain((int?)(int)MatchedMatchStatus.Active), "The active match's row before its thread");
            Assert.That(history, Is.All.AnyOf((int)MatchedMatchStatus.Active, (int)MatchedMatchStatus.Reported, (int)MatchedMatchStatus.Canceled),
                "Every history row has a real status, filled the same way");
        }
    }

    [Test]
    public async Task A_vote_message_with_no_thread_is_cleared_before_the_check_and_one_with_its_thread_is_kept()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_MatchedMatchVoteMessageNeedsThread"))) - 1]);

        // A bad row the check would refuse, past the class as only something outside it could write one
        var orphan = MatchedMatch.Start([], []);
        _context.MatchedMatches.Add(orphan);
        _context.Entry(orphan).Property(m => m.VoteMessageID).CurrentValue = 7UL;
        var posted = MatchedMatch.Start([], []).Announced(5, 8);
        _context.MatchedMatches.Add(posted);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var messages = await _context.MatchedMatches.ToDictionaryAsync(m => m.Id, m => (m.ThreadID, m.VoteMessageID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages[orphan.Id], Is.EqualTo(((ulong?)null, (ulong?)null)));
            Assert.That(messages[posted.Id], Is.EqualTo(((ulong?)5, (ulong?)8)));
        }
    }

    /// <summary>The model before MatchedMatchStatus, for a database migrated to before it: Active, not Status.</summary>
    private sealed class ContextBeforeStatus(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var match = modelBuilder.Entity<MatchedMatch>();
            match.Ignore(m => m.Status);
            match.Ignore(m => m.WinnerSide);
            match.Property<bool>(ActiveColumn);
        }
    }
}
