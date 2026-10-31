using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.HaloInfinite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Surprenant.Grunt.Models;

namespace GrifballWebApp.Test;

/// <summary>The Halo Infinite token and authorization code in the database, encrypted, shared by every process.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloTokenStoreTests
{
    private GrifballContext _context;
    private IDbContextFactory<GrifballContext> _contexts;
    private FakeTimeProvider _time;
    private IDataProtectionProvider _keys;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _contexts = GrifballContextTestExtensions.FactoryFor(_context);
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        _keys = new EphemeralDataProtectionProvider();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private HaloTokenStore Store(IDataProtectionProvider? keys = null)
        => new(_contexts, keys ?? _keys, _time, NullLogger<HaloTokenStore>.Instance);

    internal static OAuthToken Token(string access = "access-1", string refresh = "refresh-1", int expiresIn = 3600) => new()
    {
        TokenType = "bearer",
        ExpiresIn = expiresIn,
        Scope = "Xboxlive.signin Xboxlive.offline_access",
        AccessToken = access,
        RefreshToken = refresh,
        UserId = "user-1",
    };

    [Test]
    public async Task SetToken_ThenGetToken_ReturnsIt_WithItsExpiry()
    {
        await Store().SetTokenAsync(Token());

        var stored = await Store().GetTokenAsync();

        Assert.That(stored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Token.AccessToken, Is.EqualTo("access-1"));
            Assert.That(stored.Token.RefreshToken, Is.EqualTo("refresh-1"));
            Assert.That(stored.Token.UserId, Is.EqualTo("user-1"));
            Assert.That(stored.ExpiresAt, Is.EqualTo(new DateTime(2026, 10, 31, 13, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task SetToken_WithUnknownExpiry_StoresNoExpiry()
    {
        await Store().SetTokenAsync(Token(), expiryKnown: false);

        Assert.That((await Store().GetTokenAsync())!.ExpiresAt, Is.Null);
    }

    [Test]
    public async Task SetToken_WithoutExpiresIn_StoresNoExpiry()
    {
        await Store().SetTokenAsync(Token(expiresIn: 0));

        Assert.That((await Store().GetTokenAsync())!.ExpiresAt, Is.Null);
    }

    [Test]
    public async Task SetToken_Twice_KeepsTheNewer()
    {
        await Store().SetTokenAsync(Token("old"));
        _time.Advance(TimeSpan.FromMinutes(30));
        await Store().SetTokenAsync(Token("new"));

        var stored = await Store().GetTokenAsync();
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(stored!.Token.AccessToken, Is.EqualTo("new"));
            Assert.That(stored.ExpiresAt, Is.EqualTo(new DateTime(2026, 10, 31, 13, 30, 0, DateTimeKind.Utc)));
            Assert.That(await _context.HaloInfiniteCredentials.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheDatabase_HoldsOnlyTheEncryptedValue()
    {
        await Store().SetTokenAsync(Token("secret-access", "secret-refresh"));
        await Store().SetCodeAsync("secret-code");

        var rows = await _context.HaloInfiniteCredentials.AsNoTracking().ToListAsync();
        Assert.That(rows.Select(r => r.Name), Is.EquivalentTo(new[] { HaloInfiniteCredential.OAuthTokenName, HaloInfiniteCredential.AuthorizationCodeName }));
        foreach (var row in rows)
            Assert.That(row.ProtectedValue, Does.Not.Contain("secret"));
        // The app's protector reads it back; it is Data Protection's format, not just encoded.
        var protector = _keys.CreateProtector(HaloTokenStore.Purpose);
        Assert.That(protector.Unprotect(rows.Single(r => r.Name == HaloInfiniteCredential.AuthorizationCodeName).ProtectedValue), Is.EqualTo("secret-code"));
    }

    [Test]
    public async Task AnotherKeyRing_CannotReadIt_AndSeesNoToken()
    {
        await Store().SetTokenAsync(Token());

        Assert.That(await Store(new EphemeralDataProtectionProvider()).GetTokenAsync(), Is.Null);
    }

    [Test]
    public async Task TakeCode_ReturnsTheCodeOnce()
    {
        await Store().SetCodeAsync("code-1");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Store().TakeCodeAsync(), Is.EqualTo("code-1"));
            Assert.That(await Store().TakeCodeAsync(), Is.Null);
        });
    }

    [Test]
    public async Task TakeCode_FromManyProcessesAtOnce_GivesItToOne()
    {
        await Store().SetCodeAsync("code-1");

        var taken = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Store().TakeCodeAsync())));

        Assert.That(taken.Count(c => c == "code-1"), Is.EqualTo(1));
        Assert.That(taken.Count(c => c is null), Is.EqualTo(7));
    }

    [Test]
    public async Task SetCode_FromManyProcessesAtOnce_LeavesOneRow()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => Store().SetCodeAsync($"code-{i}"))));

        Assert.That(await _context.HaloInfiniteCredentials.CountAsync(), Is.EqualTo(1));
        Assert.That(await Store().TakeCodeAsync(), Does.StartWith("code-"));
    }

    [Test]
    public async Task Clear_RemovesTokenAndCode_ButNotTheImportMark()
    {
        var store = Store();
        await store.SetTokenAsync(Token());
        await store.SetCodeAsync("code-1");
        await store.MarkLegacyFilesImportedAsync();

        await store.ClearAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await store.GetTokenAsync(), Is.Null);
            Assert.That(await store.TakeCodeAsync(), Is.Null);
            Assert.That(await store.LegacyFilesImportedAsync(), Is.True);
        });
    }

    [Test]
    public async Task TheMigration_CreatesTheTable_AndItsDown_DropsIt()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        var index = migrations.IndexOf(migrations.Single(m => m.EndsWith("_AddHaloInfiniteCredentials")));

        await migrator.MigrateAsync(migrations[index - 1]);
        Assert.That(async () => await _context.HaloInfiniteCredentials.CountAsync(), Throws.Exception, "No table before");

        await migrator.MigrateAsync();
        await Store().SetTokenAsync(Token());
        Assert.That(await _context.HaloInfiniteCredentials.CountAsync(), Is.EqualTo(1));
    }
}
