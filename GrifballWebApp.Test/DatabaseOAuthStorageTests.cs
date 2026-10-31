using GrifballWebApp.Server.HaloInfinite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Surprenant.Grunt.Models;

namespace GrifballWebApp.Test;

/// <summary>
/// Grunt's storage on the token store: the process that owns the sign-in reads all of it; the others never get the
/// refresh token, see an expired token as none, and can't take an authorization code.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DatabaseOAuthStorageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static (IHaloTokenStore Store, DatabaseOAuthStorage Storage) Storage(bool ownsRefresh, StoredOAuthToken? stored)
    {
        var store = Substitute.For<IHaloTokenStore>();
        store.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(stored);
        return (store, new DatabaseOAuthStorage(store, new HaloTokenOwnership(ownsRefresh), new FakeTimeProvider(Now), NullLogger<DatabaseOAuthStorage>.Instance));
    }

    private static StoredOAuthToken Stored(DateTime? expiresAt) => new(HaloTokenStoreTests.Token(), expiresAt);

    [Test]
    public async Task TheOwner_GetsTheWholeToken_EvenExpired()
    {
        var (_, storage) = Storage(ownsRefresh: true, Stored(Now.UtcDateTime.AddMinutes(-5)));

        var token = await storage.GetToken();

        Assert.That(token!.RefreshToken, Is.EqualTo("refresh-1"));
    }

    [Test]
    public async Task AReader_GetsTheAccessToken_WithoutTheRefreshToken()
    {
        var (_, storage) = Storage(ownsRefresh: false, Stored(Now.UtcDateTime.AddMinutes(30)));

        var token = await storage.GetToken();

        Assert.Multiple(() =>
        {
            Assert.That(token!.AccessToken, Is.EqualTo("access-1"));
            Assert.That(token.RefreshToken, Is.Empty);
            Assert.That(token.UserId, Is.EqualTo("user-1"));
            Assert.That(token.TokenType, Is.EqualTo("bearer"));
            Assert.That(token.Scope, Is.Not.Empty);
            Assert.That(token.ExpiresIn, Is.EqualTo(3600));
        });
    }

    [Test]
    public async Task AReader_SeesAnExpiredTokenAsNone()
    {
        var (_, storage) = Storage(ownsRefresh: false, Stored(Now.UtcDateTime));

        Assert.That(await storage.GetToken(), Is.Null);
    }

    [Test]
    public async Task AReader_UsesATokenOfUnknownExpiry()
    {
        var (_, storage) = Storage(ownsRefresh: false, Stored(null));

        Assert.That((await storage.GetToken())!.AccessToken, Is.EqualTo("access-1"));
    }

    [Test]
    public async Task NoToken_IsNull([Values] bool ownsRefresh)
    {
        var (_, storage) = Storage(ownsRefresh, null);

        Assert.That(await storage.GetToken(), Is.Null);
    }

    [Test]
    public async Task SetToken_StoresIt()
    {
        var (store, storage) = Storage(ownsRefresh: true, null);
        var token = HaloTokenStoreTests.Token();

        await storage.SetToken(token);

        await store.Received(1).SetTokenAsync(token, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheOwner_TakesTheCode()
    {
        var store = Substitute.For<IHaloTokenStore>();
        store.TakeCodeAsync(Arg.Any<CancellationToken>()).Returns("code-1");
        var authorization = new DatabaseAccountAuthorization(store, new HaloTokenOwnership(true), NullLogger<DatabaseAccountAuthorization>.Instance);

        Assert.That(await authorization.GetCodeAsync(), Is.EqualTo("code-1"));
    }

    [Test]
    public async Task TheOwner_WithoutACode_AsksForTheAdmin()
    {
        var store = Substitute.For<IHaloTokenStore>();
        store.TakeCodeAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var authorization = new DatabaseAccountAuthorization(store, new HaloTokenOwnership(true), NullLogger<DatabaseAccountAuthorization>.Instance);

        var ex = await Assert.ThrowsAsync<Exception>(() => authorization.GetCodeAsync());
        Assert.That(ex!.Message, Is.EqualTo("Missing AccountAuthorization. Contact admin"));
    }

    [Test]
    public async Task AReader_NeverTakesTheCode_ButStoresOne()
    {
        var store = Substitute.For<IHaloTokenStore>();
        var authorization = new DatabaseAccountAuthorization(store, new HaloTokenOwnership(false), NullLogger<DatabaseAccountAuthorization>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => authorization.GetCodeAsync());
        await authorization.SetCodeAsync("code-1");

        await store.DidNotReceiveWithAnyArgs().TakeCodeAsync(default);
        await store.Received(1).SetCodeAsync("code-1", Arg.Any<CancellationToken>());
    }
}
