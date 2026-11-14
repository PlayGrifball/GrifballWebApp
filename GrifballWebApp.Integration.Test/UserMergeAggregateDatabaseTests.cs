using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeAggregateDatabaseTests
{
    private static readonly DateTime MergedAt = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task The_database_refuses_a_record_no_merge_makes()
    {
        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<UserMerge>> change)
        {
            await using var ctx = _context.NewContextLike();
            var entry = ctx.UserMerges.Add(UserMergeTestData.Merge());
            change(entry);
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
            Assert.That(await Refused(e => e.Property(m => m.TargetUserID).CurrentValue = e.Entity.SourceUserID), Is.True, "A user into itself");
            Assert.That(await Refused(e => e.Property(m => m.UndoNote).CurrentValue = "a note"), Is.True, "A note on a standing merge");
            Assert.That(await Refused(e => e.Property(m => m.UndoneByUserID).CurrentValue = 9), Is.True, "An undoer of a standing merge");
            Assert.That(await Refused(e => e.Property(m => m.RowsMoved).CurrentValue = -1), Is.True);
            Assert.That(await Refused(e => e.Property(m => m.RowsRemoved).CurrentValue = -1), Is.True);
            Assert.That(await Refused(e => e.Property(m => m.RowsAdded).CurrentValue = -1), Is.True);

            Assert.That(await Refused(_ => { }), Is.False, "A standing merge");
            Assert.That(await Refused(e => e.Entity.Undo(MergedAt, 9, "wrong person")), Is.False, "An undone one");
            Assert.That(await Refused(e => e.Entity.Undo(MergedAt, null, null)), Is.False, "Undone by nobody signed in, no note");
        }
    }

    [Test]
    public async Task Standing_and_LaterStandingMergesOfItsUsers_in_a_query_agree_with_the_loaded_records()
    {
        // Made before the first, of its users: never later
        var before = UserMergeTestData.Merge(source: 2, target: 9);
        _context.UserMerges.Add(before);
        await _context.SaveChangesAsync();
        var first = UserMergeTestData.Merge(source: 1, target: 2);
        var intoTheTarget = UserMergeTestData.Merge(source: 3, target: 2);
        var ofTheTargetOut = UserMergeTestData.Merge(source: 2, target: 4);
        var intoTheSource = UserMergeTestData.Merge(source: 5, target: 1);
        var undone = UserMergeTestData.Merge(source: 1, target: 6);
        undone.Undo(MergedAt, null, null);
        var unrelated = UserMergeTestData.Merge(source: 7, target: 8);
        _context.UserMerges.Add(first);
        await _context.SaveChangesAsync();
        _context.UserMerges.AddRange(intoTheTarget, ofTheTargetOut, intoTheSource, undone, unrelated);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var standing = await _context.UserMerges.Where(UserMerge.Standing).Select(m => m.UserMergeID).ToListAsync();
        var later = await _context.UserMerges.Where(first.LaterStandingMergesOfItsUsers()).Select(m => m.UserMergeID).ToListAsync();
        var loaded = await _context.UserMerges.AsNoTracking().ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(standing, Is.EquivalentTo(loaded.Where(m => m.IsStanding).Select(m => m.UserMergeID)));
            Assert.That(standing, Does.Not.Contain(undone.UserMergeID));
            Assert.That(later, Is.EquivalentTo(new[] { intoTheTarget.UserMergeID, ofTheTargetOut.UserMergeID, intoTheSource.UserMergeID }));
            Assert.That(later, Is.EquivalentTo(loaded.Where(first.LaterStandingMergesOfItsUsers().Compile()).Select(m => m.UserMergeID)));
        }
    }

    [Test]
    public async Task The_migration_settles_the_rows_from_before_and_keeps_the_legal_ones()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_UserMergeAggregate"))) - 1]);

        // Through the model of the day: the merges had no history yet
        await using var old = BeforeHistoryForRemainingTablesContext.On(_context);
        // Rows as the old model's public setters could leave them
        var rows = new Dictionary<string, UserMerge>();
        void Row(string name, Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<UserMerge>>? change = null, bool undo = false)
        {
            var merge = UserMergeTestData.Merge(source: 100 + rows.Count, target: 200 + rows.Count);
            if (undo)
                merge.Undo(MergedAt, 9, "wrong person");
            var entry = old.UserMerges.Add(merge);
            change?.Invoke(entry);
            rows[name] = merge;
        }
        Row("standing");
        Row("undone", undo: true);
        Row("into itself", e => e.Property(m => m.TargetUserID).CurrentValue = e.Entity.SourceUserID);
        Row("a note but standing", e =>
        {
            e.Property(m => m.UndoneByUserID).CurrentValue = 9;
            e.Property(m => m.UndoNote).CurrentValue = "a note";
        });
        Row("negative counts", e =>
        {
            e.Property(m => m.RowsMoved).CurrentValue = -1;
            e.Property(m => m.RowsRemoved).CurrentValue = -2;
            e.Property(m => m.RowsAdded).CurrentValue = -3;
        });
        await old.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.UserMerges.AsNoTracking().ToDictionaryAsync(m => m.UserMergeID);
        UserMerge Of(string name) => after[rows[name].UserMergeID];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((Of("standing").IsStanding, Of("standing").RowsMoved, Of("standing").Reason), Is.EqualTo((true, 3, "claim 12")), "Legal: kept");
            Assert.That((Of("undone").UndoneAt, Of("undone").UndoneByUserID, Of("undone").UndoNote), Is.EqualTo(((DateTime?)MergedAt, (int?)9, "wrong person")), "Legal: kept");
            Assert.That(after.ContainsKey(rows["into itself"].UserMergeID), Is.False, "Records no merge: removed");
            Assert.That((Of("a note but standing").UndoneAt, Of("a note but standing").UndoneByUserID, Of("a note but standing").UndoNote),
                Is.EqualTo(((DateTime?)null, (int?)null, (string?)null)), "Still standing: the undo it never had is cleared");
            Assert.That((Of("negative counts").RowsMoved, Of("negative counts").RowsRemoved, Of("negative counts").RowsAdded), Is.EqualTo((0, 0, 0)));
        }
    }
}
