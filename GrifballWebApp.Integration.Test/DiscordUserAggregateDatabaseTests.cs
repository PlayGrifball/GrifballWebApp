using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The database's half of a Discord account (#351): it refuses a blank username, and the DiscordUserAggregate migration
/// gives the blank ones from before their ID, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordUserAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task The_database_refuses_a_blank_username()
    {
        var n = 0;

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(string username)
        {
            await using var ctx = _context.NewContextLike();
            var entry = ctx.DiscordUsers.Add(DiscordUser.FromDiscord(++n, "name"));
            entry.Property(d => d.DiscordUsername).CurrentValue = username;
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
            Assert.That(await Refused(""), Is.True, "Empty");
            Assert.That(await Refused("   "), Is.True, "Spaces");
            Assert.That(await Refused("alice"), Is.False);
            Assert.That(await Refused(" bob "), Is.False, "A name with spaces around it is still a name");
        });
    }

    [Test]
    public async Task Renaming_is_saved()
    {
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(7, "old"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var account = await _context.DiscordUsers.SingleAsync(d => d.DiscordUserID == 7);
        Assert.That(account.Rename("new"), Is.EqualTo(DiscordRename.Renamed));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.That((await _context.DiscordUsers.AsNoTracking().SingleAsync(d => d.DiscordUserID == 7)).DiscordUsername, Is.EqualTo("new"));
    }

    [Test]
    public async Task The_migration_gives_blank_usernames_their_ID_and_keeps_the_others()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_DiscordUserAggregate"))) - 1]);

        // Rows as the old model's public setters could leave them
        void Row(long id, string username)
        {
            var entry = _context.DiscordUsers.Add(DiscordUser.FromDiscord(id, "name"));
            entry.Property(d => d.DiscordUsername).CurrentValue = username;
        }
        Row(1, "alice");
        Row(2, "");
        Row(123456789012345678, "   ");
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var names = await _context.DiscordUsers.AsNoTracking().ToDictionaryAsync(d => d.DiscordUserID, d => d.DiscordUsername);
        Assert.Multiple(() =>
        {
            Assert.That(names[1], Is.EqualTo("alice"), "Left alone");
            Assert.That(names[2], Is.EqualTo("2"), "Empty: its ID");
            Assert.That(names[123456789012345678], Is.EqualTo("123456789012345678"), "Spaces: its ID");
        });
    }
}
