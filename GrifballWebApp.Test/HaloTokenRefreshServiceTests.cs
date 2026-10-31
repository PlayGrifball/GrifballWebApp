using GrifballWebApp.Database;
using GrifballWebApp.Server.HaloInfinite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Surprenant.Grunt.Models;
using System.Text.Json;
using Outcome = GrifballWebApp.Server.HaloInfinite.HaloTokenRefreshService.Outcome;

namespace GrifballWebApp.Test;

/// <summary>The one process that owns the Halo Infinite sign-in: code exchange, refresh before expiry, the one-time import.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloTokenRefreshServiceTests
{
    private GrifballContext _context;
    private FakeTimeProvider _time;
    private HaloTokenStore _store;
    private IXboxTokenClient _client;
    private string _directory;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        _store = new HaloTokenStore(GrifballContextTestExtensions.FactoryFor(_context), new EphemeralDataProtectionProvider(), _time, NullLogger<HaloTokenStore>.Instance);
        _client = Substitute.For<IXboxTokenClient>();
        _directory = Path.Combine(Path.GetTempPath(), "grif-halo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public async Task TearDown()
    {
        Directory.Delete(_directory, recursive: true);
        await _context.DropDatabaseAndDispose();
    }

    private HaloTokenRefreshService Service(Dictionary<string, string?>? settings = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HaloInfinite:LegacyDirectory"] = _directory,
        }.Concat(settings ?? [])).Build();
        return new HaloTokenRefreshService(_store, _client, _time, config, NullLogger<HaloTokenRefreshService>.Instance);
    }

    private static OAuthToken Token(string access = "access-1", string refresh = "refresh-1") => HaloTokenStoreTests.Token(access, refresh);

    [Test]
    public async Task NoTokenAndNoCode_DoesNothing()
    {
        Assert.That(await Service().RunOnceAsync(), Is.EqualTo(Outcome.NotSignedIn));
        await _client.DidNotReceiveWithAnyArgs().RequestTokenAsync(default!);
    }

    [Test]
    public async Task APendingCode_IsExchangedForAToken_Once()
    {
        await _store.SetCodeAsync("code-1");
        _client.RequestTokenAsync("code-1").Returns(Token());
        var service = Service();

        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.SignedIn));
        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.Fresh));

        await _client.Received(1).RequestTokenAsync("code-1");
        Assert.That((await _store.GetTokenAsync())!.Token.AccessToken, Is.EqualTo("access-1"));
    }

    [Test]
    public async Task ARefusedCode_IsUsedUp_AndLeavesNoToken()
    {
        await _store.SetCodeAsync("bad-code");
        _client.RequestTokenAsync("bad-code").Returns((OAuthToken?)null);

        Assert.That(await Service().RunOnceAsync(), Is.EqualTo(Outcome.Failed));
        Assert.That(await _store.GetTokenAsync(), Is.Null);
        Assert.That(await _store.TakeCodeAsync(), Is.Null);
    }

    [Test]
    public async Task AFreshToken_IsLeftAlone_UntilItNearsItsExpiry()
    {
        await _store.SetTokenAsync(Token());
        _client.RefreshTokenAsync("refresh-1").Returns(Token("access-2", "refresh-2"));
        var service = Service();

        _time.Advance(TimeSpan.FromMinutes(44));
        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.Fresh));
        await _client.DidNotReceiveWithAnyArgs().RefreshTokenAsync(default!);

        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.Refreshed));
        var stored = (await _store.GetTokenAsync())!;
        Assert.Multiple(() =>
        {
            Assert.That(stored.Token.AccessToken, Is.EqualTo("access-2"));
            Assert.That(stored.Token.RefreshToken, Is.EqualTo("refresh-2"));
            Assert.That(stored.ExpiresAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime.AddHours(1)));
        });
    }

    [Test]
    public async Task ATokenOfUnknownExpiry_IsRefreshedAtOnce()
    {
        await _store.SetTokenAsync(Token(), expiryKnown: false);
        _client.RefreshTokenAsync("refresh-1").Returns(Token("access-2", "refresh-2"));

        Assert.That(await Service().RunOnceAsync(), Is.EqualTo(Outcome.Refreshed));
        Assert.That((await _store.GetTokenAsync())!.ExpiresAt, Is.Not.Null);
    }

    [Test]
    public async Task ARefreshWithoutANewRefreshToken_KeepsTheOldOne()
    {
        await _store.SetTokenAsync(Token(), expiryKnown: false);
        _client.RefreshTokenAsync("refresh-1").Returns(Token("access-2", refresh: ""));

        await Service().RunOnceAsync();

        Assert.That((await _store.GetTokenAsync())!.Token.RefreshToken, Is.EqualTo("refresh-1"));
    }

    [Test]
    public async Task ARefusedRefresh_KeepsTheToken_AndWaitsBeforeTryingAgain()
    {
        await _store.SetTokenAsync(Token(), expiryKnown: false);
        _client.RefreshTokenAsync("refresh-1").Returns((OAuthToken?)null);
        var service = Service(new() { ["HaloInfinite:TokenRefresh:RetryAfter"] = "00:05:00" });

        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.Failed));
        _time.Advance(TimeSpan.FromMinutes(4));
        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.Waiting));
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.That(await service.RunOnceAsync(), Is.EqualTo(Outcome.Failed));

        await _client.Received(2).RefreshTokenAsync("refresh-1");
        Assert.That((await _store.GetTokenAsync())!.Token.AccessToken, Is.EqualTo("access-1"));
    }

    [Test]
    public async Task ATokenWithoutARefreshToken_CannotBeRefreshed()
    {
        await _store.SetTokenAsync(Token(refresh: ""), expiryKnown: false);

        Assert.That(await Service().RunOnceAsync(), Is.EqualTo(Outcome.Failed));
        await _client.DidNotReceiveWithAnyArgs().RefreshTokenAsync(default!);
    }

    [Test]
    public void Settings_AreRead_AndBadOnesFallBackToTheDefaults()
    {
        var set = Service(new()
        {
            ["HaloInfinite:TokenRefresh:CheckInterval"] = "00:00:30",
            ["HaloInfinite:TokenRefresh:RefreshBefore"] = "00:20:00",
            ["HaloInfinite:TokenRefresh:RetryAfter"] = "00:02:00",
        });
        var bad = Service(new()
        {
            ["HaloInfinite:TokenRefresh:CheckInterval"] = "soon",
            ["HaloInfinite:TokenRefresh:RefreshBefore"] = "-00:01:00",
        });
        Assert.Multiple(() =>
        {
            Assert.That(set.CheckInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(set.RefreshBefore, Is.EqualTo(TimeSpan.FromMinutes(20)));
            Assert.That(set.RetryAfter, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(bad.CheckInterval, Is.EqualTo(HaloTokenRefreshService.DefaultCheckInterval));
            Assert.That(bad.RefreshBefore, Is.EqualTo(HaloTokenRefreshService.DefaultRefreshBefore));
            Assert.That(bad.RetryAfter, Is.EqualTo(HaloTokenRefreshService.DefaultRetryAfter));
        });
    }

    [Test]
    public async Task Import_TakesTokensJson_Once_AndLeavesTheFile()
    {
        // As Grunt's file storage wrote it.
        var file = Path.Combine(_directory, HaloTokenRefreshService.TokenFile);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(Token("legacy-access", "legacy-refresh")));
        var service = Service();

        Assert.That(await service.ImportLegacyFilesAsync(), Is.True);
        var stored = (await _store.GetTokenAsync())!;
        Assert.Multiple(() =>
        {
            Assert.That(stored.Token.AccessToken, Is.EqualTo("legacy-access"));
            Assert.That(stored.Token.RefreshToken, Is.EqualTo("legacy-refresh"));
            Assert.That(stored.ExpiresAt, Is.Null, "Its age is unknown");
            Assert.That(File.Exists(file), Is.True, "Left for an older version");
        });

        // Signed out afterwards: a restart doesn't bring the old token back.
        await _store.ClearAsync();
        Assert.That(await Service().ImportLegacyFilesAsync(), Is.False);
        Assert.That(await _store.GetTokenAsync(), Is.Null);
    }

    [Test]
    public async Task Import_TakesAPendingCode_WhenThereIsNoToken()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.CodeFile), "legacy-code\n");

        Assert.That(await Service().ImportLegacyFilesAsync(), Is.True);
        Assert.That(await _store.TakeCodeAsync(), Is.EqualTo("legacy-code"));
    }

    [Test]
    public async Task Import_KeepsATokenAlreadyInTheDatabase()
    {
        await _store.SetTokenAsync(Token("current"));
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.TokenFile), JsonSerializer.Serialize(Token("legacy")));
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.CodeFile), "legacy-code");

        Assert.That(await Service().ImportLegacyFilesAsync(), Is.False);
        Assert.That((await _store.GetTokenAsync())!.Token.AccessToken, Is.EqualTo("current"));
        Assert.That(await _store.TakeCodeAsync(), Is.Null);
    }

    [Test]
    public async Task Import_WithoutFiles_DoesNothing()
    {
        Assert.That(await Service().ImportLegacyFilesAsync(), Is.False);
        Assert.That(await _store.LegacyFilesImportedAsync(), Is.False);
    }

    [Test]
    public async Task AsAHostedService_ImportsThenSignsIn()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.TokenFile), JsonSerializer.Serialize(Token("legacy-access", "legacy-refresh")));
        var refreshed = new TaskCompletionSource();
        _client.RefreshTokenAsync("legacy-refresh").Returns(_ => { refreshed.TrySetResult(); return Token("access-2", "refresh-2"); });
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await service.StopAsync(CancellationToken.None);

        Assert.That(await _store.LegacyFilesImportedAsync(), Is.True);
    }

    [Test]
    public async Task Import_SkipsATokenFileWithoutAToken_AndAnEmptyCodeFile()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.TokenFile), "{}");
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.CodeFile), " \n");

        Assert.That(await Service().ImportLegacyFilesAsync(), Is.False);
        Assert.That(await _store.GetTokenAsync(), Is.Null);
        Assert.That(await _store.TakeCodeAsync(), Is.Null);
        Assert.That(await _store.LegacyFilesImportedAsync(), Is.True);
    }

    [Test]
    public void TheLegacyDirectory_DefaultsToTheWorkingDirectory()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new HaloTokenRefreshService(_store, _client, _time, config, NullLogger<HaloTokenRefreshService>.Instance);

        Assert.That(service.CheckInterval, Is.EqualTo(HaloTokenRefreshService.DefaultCheckInterval));
    }

    [Test]
    public async Task AsAHostedService_AnUnreadableTokenFile_DoesNotStopTheRefresh()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, HaloTokenRefreshService.TokenFile), "not json");
        await _store.SetCodeAsync("code-1");
        var signedIn = new TaskCompletionSource();
        _client.RequestTokenAsync("code-1").Returns(_ => { signedIn.TrySetResult(); return Token(); });
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        await signedIn.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await service.StopAsync(CancellationToken.None);

        Assert.That(await _store.LegacyFilesImportedAsync(), Is.False, "The import failed and will be tried again at the next start");
    }
}
