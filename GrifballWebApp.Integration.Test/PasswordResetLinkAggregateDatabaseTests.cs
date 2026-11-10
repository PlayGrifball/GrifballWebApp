using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The database's half of a reset link's state (#352): it refuses a use at or after the link's expiry, the expressions agree
/// with the loaded link, and the PasswordResetLinkAggregate migration dates the links used before, alike on whichever
/// database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PasswordResetLinkAggregateDatabaseTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<int> SeedUser(string userName = "resetuser")
    {
        var user = User.Seed(userName: userName);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    [Test]
    public async Task The_database_refuses_a_use_at_or_after_expiry()
    {
        var userId = await SeedUser();
        var n = 0;

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(DateTime? usedAt)
        {
            await using var ctx = _context.NewContextLike();
            var entry = ctx.PasswordResetLinks.Add(PasswordResetLink.Seed(userId, $"token{n++}", Now));
            entry.Property(l => l.UsedAt).CurrentValue = usedAt;
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
            Assert.That(await Refused(Now), Is.True, "Used as it expired");
            Assert.That(await Refused(Now.AddMinutes(1)), Is.True, "Used after it expired");

            Assert.That(await Refused(Now.AddMinutes(-1)), Is.False, "Used before it expired");
            Assert.That(await Refused(null), Is.False, "Unused");
        });
    }

    [Test]
    public async Task Usable_and_Spent_in_a_query_agree_with_the_loaded_link()
    {
        var userId = await SeedUser();
        var (used, _) = PasswordResetLink.Issue(userId, Now.AddMinutes(-5));
        used.Use(Now.AddMinutes(-1));
        var (replaced, _) = PasswordResetLink.Issue(userId, Now.AddMinutes(-5));
        replaced.Expire(Now.AddMinutes(-1));
        var links = new[]
        {
            PasswordResetLink.Seed(userId, "usable", Now.AddMinutes(5)),
            PasswordResetLink.Seed(userId, "expired", Now.AddMinutes(-5)),
            PasswordResetLink.Seed(userId, "expiring now", Now),
            used,
            replaced,
        };
        _context.PasswordResetLinks.AddRange(links);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var usable = await _context.PasswordResetLinks.AsNoTracking().Where(PasswordResetLink.Usable(Now)).Select(l => l.Token).ToListAsync();
        var spent = await _context.PasswordResetLinks.AsNoTracking().Where(PasswordResetLink.Spent(Now)).Select(l => l.Token).ToListAsync();
        var loaded = await _context.PasswordResetLinks.AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(usable, Is.EqualTo(new[] { PasswordResetLink.HashToken("usable") }));
            Assert.That(loaded.Where(l => l.IsUsableAt(Now)).Select(l => l.Token), Is.EquivalentTo(usable), "The query and the loaded link agree");
            Assert.That(spent, Is.EquivalentTo(loaded.Where(l => !l.IsUsableAt(Now)).Select(l => l.Token)), "Spent is every link not usable");
            Assert.That(spent, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public async Task Found_by_its_token_not_by_the_hash_given_as_a_token()
    {
        var userId = await SeedUser();
        _context.PasswordResetLinks.Add(PasswordResetLink.Seed(userId, "giventoken", Now));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.PasswordResetLinks.CountAsync(PasswordResetLink.OfToken("giventoken")), Is.EqualTo(1));
            Assert.That(await _context.PasswordResetLinks.CountAsync(PasswordResetLink.OfToken(PasswordResetLink.HashToken("giventoken"))), Is.Zero,
                "A read of the table gives no token that works");
        });
    }

    [Test, NonParallelizable] // Its Down alters a system-versioned table, which deadlocks with another Down on SQL Server
    public async Task The_migration_dates_the_used_links_and_Down_marks_them_used_again()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var before = migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_PasswordResetLinkAggregate"))) - 1];
        await migrator.MigrateAsync(before);

        var userId = await SeedUser();
        var created = Now.AddMinutes(-30);
        // Written in SQL: this schema has IsUsed, not the model's UsedAt
        await InsertOld("unused", Now.AddMinutes(10), false, created);
        await InsertOld("used in time", Now.AddMinutes(10), true, Now.AddMinutes(-1));
        await InsertOld("used, when unknown", Now.AddMinutes(-10), true, Now.AddMinutes(-1));

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.PasswordResetLinks.AsNoTracking().ToDictionaryAsync(l => l.Token, l => l.UsedAt);
        Assert.Multiple(() =>
        {
            Assert.That(after, Has.Count.EqualTo(2), "The use that can't be dated before expiry is deleted: it was spent");
            Assert.That(after[PasswordResetLink.HashToken("unused")], Is.Null);
            Assert.That(after[PasswordResetLink.HashToken("used in time")], Is.EqualTo(Now.AddMinutes(-1)), "Used when last changed");
        });

        await migrator.MigrateAsync(before);
        var isUsed = await _context.Database.SqlQueryRaw<bool>(Sql(
                "SELECT [IsUsed] AS [Value] FROM [Auth].[PasswordResetLinks] ORDER BY [Id]",
                """SELECT "IsUsed" AS "Value" FROM "Auth"."PasswordResetLinks" ORDER BY "Id" """,
                """SELECT "IsUsed" AS "Value" FROM "PasswordResetLinks" ORDER BY "Id" """))
            .ToListAsync();
        Assert.That(isUsed, Is.EqualTo(new[] { false, true }), "Down: a dated link is used");

        await migrator.MigrateAsync();

        Task InsertOld(string token, DateTime expiresAt, bool isUsed, DateTime modifiedAt) => _context.Database.ExecuteSqlRawAsync(Sql(
                "INSERT INTO [Auth].[PasswordResetLinks] ([UserId], [Token], [ExpiresAt], [IsUsed], [CreatedAt], [ModifiedAt]) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                """INSERT INTO "Auth"."PasswordResetLinks" ("UserId", "Token", "ExpiresAt", "IsUsed", "CreatedAt", "ModifiedAt") VALUES ({0}, {1}, {2}, {3}, {4}, {5})""",
                """INSERT INTO "PasswordResetLinks" ("UserId", "Token", "ExpiresAt", "IsUsed", "CreatedAt", "ModifiedAt") VALUES ({0}, {1}, {2}, {3}, {4}, {5})"""),
            userId, PasswordResetLink.HashToken(token), expiresAt, isUsed, created, modifiedAt);
    }

    private static string Sql(string sqlServer, string postgres, string sqlite) => TestDatabase.Provider switch
    {
        DatabaseProvider.SqlServer => sqlServer,
        DatabaseProvider.Postgres => postgres,
        _ => sqlite,
    };
}
