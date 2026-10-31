using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>The BackfillPlayerRole migration, on whichever database the suite runs on.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackfillPlayerRoleMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task AccountsWithoutAnyRole_BecomePlayers_OthersAreLeftAlone()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var backfill = migrations.IndexOf(migrations.Single(m => m.EndsWith("_BackfillPlayerRole")));
        await migrator.MigrateAsync(migrations[backfill - 1]);

        var noRoles = new User { UserName = "botmade" };
        var dummy = new User { UserName = "dummy", IsDummyUser = true };
        var commissioner = new User { UserName = "commish" };
        _context.Users.AddRange(noRoles, dummy, commissioner);
        await _context.SaveChangesAsync();
        _context.UserRoles.Add(new UserRole { UserId = commissioner.Id, RoleId = 2 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var roles = await _context.UserRoles
            .Select(x => new { x.UserId, x.Role.Name })
            .ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(roles.Where(x => x.UserId == noRoles.Id).Select(x => x.Name), Is.EqualTo(new[] { "Player" }));
            Assert.That(roles.Where(x => x.UserId == dummy.Id), Is.Empty, "Placeholder accounts don't sign in");
            Assert.That(roles.Where(x => x.UserId == commissioner.Id).Select(x => x.Name), Is.EqualTo(new[] { "Commissioner" }), "Accounts with a role keep just theirs");
        });
    }
}
