using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>The user's rules where the database keeps them: the check constraints, and the migration settling the rows
/// from before them.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserAggregateDatabaseTests
{
    private const int West = 1;

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private record Shape(int? RegionID = null, RegionSource? RegionSource = null, int MMR = User.StartingMMR, int Wins = 0, int Losses = 0, int WinStreak = 0, int LossStreak = 0);

    private static void Set(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<User> entry, Shape shape)
    {
        entry.Property(u => u.RegionID).CurrentValue = shape.RegionID;
        entry.Property(u => u.RegionSource).CurrentValue = shape.RegionSource;
        entry.Property(u => u.MMR).CurrentValue = shape.MMR;
        entry.Property(u => u.Wins).CurrentValue = shape.Wins;
        entry.Property(u => u.Losses).CurrentValue = shape.Losses;
        entry.Property(u => u.WinStreak).CurrentValue = shape.WinStreak;
        entry.Property(u => u.LossStreak).CurrentValue = shape.LossStreak;
    }

    [Test]
    public async Task The_database_refuses_a_region_without_its_chooser_and_an_impossible_record()
    {
        var n = 0;

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(Shape shape)
        {
            await using var ctx = _context.NewContextLike();
            Set(ctx.Users.Add(User.Seed(userName: $"user{n++}")), shape);
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
            Assert.That(await Refused(new(RegionID: West)), Is.True, "A region nobody chose");
            Assert.That(await Refused(new(RegionSource: RegionSource.User)), Is.True, "A chooser of no region");
            Assert.That(await Refused(new(RegionID: West, RegionSource: (RegionSource)9)), Is.True, "A chooser the model doesn't know");
            Assert.That(await Refused(new(MMR: -1)), Is.True, "A negative MMR");
            Assert.That(await Refused(new(Wins: -1)), Is.True, "Negative wins");
            Assert.That(await Refused(new(Losses: -1)), Is.True, "Negative losses");
            Assert.That(await Refused(new(Wins: 2, Losses: 2, WinStreak: 1, LossStreak: 1)), Is.True, "Two streaks at once");
            Assert.That(await Refused(new(Wins: 1, WinStreak: 2)), Is.True, "A win streak longer than the wins");
            Assert.That(await Refused(new(Losses: 1, LossStreak: 2)), Is.True, "A loss streak longer than the losses");
            Assert.That(await Refused(new(Wins: -1, WinStreak: -1)), Is.True, "A negative streak");

            Assert.That(await Refused(new()), Is.False);
            Assert.That(await Refused(new(RegionID: West, RegionSource: RegionSource.Import)), Is.False);
            Assert.That(await Refused(new(MMR: 0, Wins: 3, Losses: 4, LossStreak: 4)), Is.False);
            Assert.That(await Refused(new(Wins: 3, Losses: 4, WinStreak: 3)), Is.False);
        }
    }

    [Test]
    public async Task A_merge_moves_the_experiences_through_the_root()
    {
        var to = User.Register("to");
        var from = User.Register("from");
        _context.Users.AddRange(to, from);
        await _context.SaveChangesAsync();
        _context.Set<UserExperience>().AddRange(new UserExperience(1) { UserID = to.Id }, new UserExperience(1) { UserID = from.Id }, new UserExperience(2) { UserID = from.Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var target = await _context.Users.Include(u => u.PersonExperiences).SingleAsync(u => u.Id == to.Id);
        var source = await _context.Users.Include(u => u.PersonExperiences).SingleAsync(u => u.Id == from.Id);
        target.TakeExperiencesFrom(source);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var rows = await _context.Set<UserExperience>().AsNoTracking().Select(e => new { e.UserID, e.GameVersionID }).ToListAsync();
        Assert.That(rows, Is.EquivalentTo(new[] { new { UserID = to.Id, GameVersionID = 1 }, new { UserID = to.Id, GameVersionID = 2 } }), "The source's rows deleted, the version the target lacked added");
    }

    [Test]
    public async Task The_migration_settles_the_rows_from_before_and_keeps_the_legal_ones()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_UserAggregate"))) - 1]);

        // Rows as the old model's public setters could leave them
        var rows = new Dictionary<string, User>();
        void Row(string name, Shape shape)
        {
            var user = User.Seed(userName: name.Replace(' ', '-'));
            Set(_context.Users.Add(user), shape);
            rows[name] = user;
        }
        Row("legal", new(RegionID: West, RegionSource: RegionSource.Sysadmin, MMR: 1100, Wins: 3, Losses: 1, WinStreak: 2));
        Row("region from before its chooser", new(RegionID: West));
        Row("unknown chooser", new(RegionID: West, RegionSource: (RegionSource)9));
        Row("chooser of nothing", new(RegionSource: RegionSource.Import));
        Row("negative", new(MMR: -5, Wins: -1, Losses: -2, WinStreak: -1, LossStreak: -1));
        Row("two streaks", new(Wins: 4, Losses: 4, WinStreak: 2, LossStreak: 1));
        Row("streaks past the record", new(Wins: 1, Losses: 3, WinStreak: 0, LossStreak: 5));
        Row("merged source", new(Wins: 0, Losses: 0, WinStreak: 2));
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id);
        Shape Of(string name)
        {
            var u = after[rows[name].Id];
            return new(u.RegionID, u.RegionSource, u.MMR, u.Wins, u.Losses, u.WinStreak, u.LossStreak);
        }
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Of("legal"), Is.EqualTo(new Shape(West, RegionSource.Sysadmin, 1100, 3, 1, 2, 0)), "Legal: kept");
            Assert.That(Of("region from before its chooser"), Is.EqualTo(new Shape(West, RegionSource.User)), "A person's choice: the import keeps it");
            Assert.That(Of("unknown chooser"), Is.EqualTo(new Shape(West, RegionSource.User)));
            Assert.That(Of("chooser of nothing"), Is.EqualTo(new Shape()), "No region, nobody's choice");
            Assert.That(Of("negative"), Is.EqualTo(new Shape(MMR: 0)), "Nothing below zero");
            Assert.That(Of("two streaks"), Is.EqualTo(new Shape(Wins: 4, Losses: 4)), "Neither streak is known: both end");
            Assert.That(Of("streaks past the record"), Is.EqualTo(new Shape(Wins: 1, Losses: 3, LossStreak: 3)), "At most every loss");
            Assert.That(Of("merged source"), Is.EqualTo(new Shape()), "A merge's source kept its streak without its games");
        }
    }
}
