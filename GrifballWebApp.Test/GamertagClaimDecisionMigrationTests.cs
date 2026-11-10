using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The GamertagClaimDecision migration (#300) fills DecidedAt and DecidedByUserID from the audit columns of each decided
/// claim's first version that isn't pending, in its history or the row itself, and settles what its constraints would
/// refuse (a rejection reason on a claim that isn't rejected); alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimDecisionMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheDecision_IsFilledFromTheFirstDecidedVersion()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_GamertagClaimDecision"))) - 1]);

        var placeholder = User.Seed(userName: "historical:Old Tag", isDummyUser: true);
        _context.Users.Add(placeholder);
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "A"), XboxUser.FromXbox(2, "B"),
            XboxUser.FromXbox(3, "C"), XboxUser.FromXbox(4, "D"));
        await _context.SaveChangesAsync();

        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var approvedAt = created.AddDays(1);
        var movedAt = created.AddDays(2);
        var rejectedAt = created.AddDays(3);
        int pending, approved, rejected, strayReason;
        // Through the model as it was then: no decision columns. The audit columns as the interceptor would have written them.
        await using (var before = new ContextBeforeDecision(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).Options))
        {
            GamertagClaim Old(long xuid)
            {
                var claim = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, xuid, "Old Tag", "OLD TAG", placeholder.Id);
                before.GamertagClaims.Add(claim);
                Stamp(claim, created, null);
                return claim;
            }
            void Stamp(GamertagClaim claim, DateTime at, int? by)
            {
                var entry = before.Entry(claim);
                entry.Property(c => c.ModifiedAt).CurrentValue = at;
                entry.Property(c => c.ModifiedByID).CurrentValue = by;
                if (entry.State == EntityState.Added)
                    entry.Property(c => c.CreatedAt).CurrentValue = at;
            }
            var stillPending = Old(1);
            var approvedThenMoved = Old(2);
            var rejectedWithReason = Old(3);
            var approvedWithReason = Old(4);
            await before.SaveChangesAsync();

            // Decided: a sysadmin approved it, and a later write (a merge moving it) stamped it again
            approvedThenMoved.Approve(7, approvedAt);
            Stamp(approvedThenMoved, approvedAt, 7);
            rejectedWithReason.Reject("Not them", null, rejectedAt);
            Stamp(rejectedWithReason, rejectedAt, null);
            approvedWithReason.Approve(8, approvedAt);
            before.Entry(approvedWithReason).Property(c => c.RejectionReason).CurrentValue = "Left over";
            Stamp(approvedWithReason, approvedAt, 8);
            await before.SaveChangesAsync();
            before.Entry(approvedThenMoved).Property(c => c.PlaceholderUserID).CurrentValue = null;
            Stamp(approvedThenMoved, movedAt, 9);
            await before.SaveChangesAsync();
            (pending, approved, rejected, strayReason) = (stillPending.GamertagClaimID, approvedThenMoved.GamertagClaimID, rejectedWithReason.GamertagClaimID, approvedWithReason.GamertagClaimID);
        }

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var claims = await _context.GamertagClaims.ToDictionaryAsync(c => c.GamertagClaimID, c => (c.Status, c.DecidedAt, c.DecidedByUserID, c.RejectionReason));
        var pendingVersions = await _context.GamertagClaims.TemporalAll().CountAsync(c => c.GamertagClaimID == pending);
        Assert.Multiple(() =>
        {
            Assert.That(claims[pending], Is.EqualTo((GamertagClaimStatus.Pending, (DateTime?)null, (int?)null, (string?)null)));
            Assert.That(claims[approved], Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)approvedAt, (int?)7, (string?)null)),
                "The approval's own write, not the later one: from the history");
            Assert.That(claims[rejected], Is.EqualTo((GamertagClaimStatus.Rejected, (DateTime?)rejectedAt, (int?)null, "Not them")),
                "Nobody signed in, from the row itself");
            Assert.That(claims[strayReason], Is.EqualTo((GamertagClaimStatus.Approved, (DateTime?)approvedAt, (int?)8, (string?)null)),
                "Only a rejected claim keeps a reason");
            Assert.That(pendingVersions, Is.EqualTo(1), "A claim with nothing to settle isn't written: no empty version in its history");
        });
    }

    /// <summary>The model before GamertagClaimDecision, for a database migrated to before it: no decision columns.</summary>
    private sealed class ContextBeforeDecision(DbContextOptions<GrifballContext> options) : GrifballContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var claim = modelBuilder.Entity<GamertagClaim>();
            claim.Ignore(c => c.DecidedAt);
            claim.Ignore(c => c.DecidedByUserID);
        }
    }
}
