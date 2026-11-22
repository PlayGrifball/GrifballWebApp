using System.Text.Json;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Integration.Test;

/// <summary>Personal access tokens (<see cref="AccessTokenService"/>) in Identity's user tokens: made, found, expired and revoked.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AccessTokenServiceTests
{
    private GrifballContext _context = null!;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
    private AccessTokenService _tokens = null!;
    private User _user = null!;
    private User _other = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _tokens = new AccessTokenService(_context, _time, new MemoryCache(new MemoryCacheOptions()));
        _user = User.Seed(userName: "token-owner", displayName: "Token Owner");
        _other = User.Seed(userName: "someone-else", displayName: "Someone Else");
        _context.Users.AddRange(_user, _other);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<CreatedAccessTokenDto> Create(User user, string name = "Claude Code", int days = 30)
    {
        var (created, refusal) = await _tokens.Create(user.Id, new CreateAccessTokenRequest(name, days));
        Assert.That(refusal, Is.Null);
        return created!;
    }

    [Test]
    public async Task AToken_SignsInTheUserWhoMadeIt_AndOnlyItsHashIsKept()
    {
        var created = await Create(_user);

        var found = await _tokens.Authenticate(created.Token);

        Assert.That(found?.User.Id, Is.EqualTo(_user.Id));
        var stored = await _context.UserTokens.AsNoTracking().SingleAsync(t => t.Name == created.AccessToken.ID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.LoginProvider, Is.EqualTo(AccessTokenService.LoginProvider));
            Assert.That(stored.Value, Does.Not.Contain(created.Token[(AccessTokenService.Prefix.Length + 33)..]), "the secret is never stored");
            Assert.That(created.AccessToken.ExpiresAt, Is.EqualTo(_time.GetUtcNow().AddDays(30)));
        }
    }

    [Test]
    public async Task AWrongSecret_AMalformedToken_OrAnUnknownID_SignsNobodyIn()
    {
        var created = await Create(_user);
        var wrongSecret = created.Token[..^1] + (created.Token[^1] == 'A' ? 'B' : 'A');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _tokens.Authenticate(wrongSecret), Is.Null, "a wrong secret");
            Assert.That(await _tokens.Authenticate(created.Token[AccessTokenService.Prefix.Length..]), Is.Null, "no prefix");
            Assert.That(await _tokens.Authenticate(AccessTokenService.Prefix + "short"), Is.Null, "too short to hold an ID");
            Assert.That(await _tokens.Authenticate(AccessTokenService.Prefix + new string('f', 32) + "_" + "secret"), Is.Null, "an unknown ID");
        }
    }

    [Test]
    public async Task AToken_StopsWorking_WhenItExpires()
    {
        var created = await Create(_user, days: 1);

        _time.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));
        Assert.That(await _tokens.Authenticate(created.Token), Is.Not.Null, "a second before");
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await _tokens.Authenticate(created.Token), Is.Null, "when it expires");
    }

    [Test]
    public async Task Revoke_RemovesOnlyTheOwnersToken()
    {
        var mine = await Create(_user);
        var theirs = await Create(_other);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _tokens.Revoke(_user.Id, theirs.AccessToken.ID), Is.False, "not someone else's");
            Assert.That(await _tokens.Revoke(_user.Id, mine.AccessToken.ID), Is.True);
        }
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _tokens.Authenticate(mine.Token), Is.Null);
            Assert.That(await _tokens.Authenticate(theirs.Token), Is.Not.Null);
        }
    }

    [Test]
    public async Task Revoke_TakesEffectImmediately_EvenThoughAuthenticateCachesTheRow()
    {
        var created = await Create(_user);
        Assert.That(await _tokens.Authenticate(created.Token), Is.Not.Null, "warms the cache for this ID");

        await _tokens.Revoke(_user.Id, created.AccessToken.ID);

        Assert.That(await _tokens.Authenticate(created.Token), Is.Null, "the cached row must not outlive the revoke");
    }

    [Test]
    public async Task ACachedToken_StillRefusesAWrongSecret()
    {
        // A cache hit must still check the secret's hash: caching by ID alone, without this, would let anything
        // claiming a cached ID in without its actual secret.
        var created = await Create(_user);
        await _tokens.Authenticate(created.Token); // warms the cache for this ID
        var wrongSecret = created.Token[..^1] + (created.Token[^1] == 'A' ? 'B' : 'A');

        Assert.That(await _tokens.Authenticate(wrongSecret), Is.Null);
    }

    [Test]
    public async Task ACachedToken_StillStopsWorking_WhenItExpires()
    {
        var created = await Create(_user, days: 1);
        Assert.That(await _tokens.Authenticate(created.Token), Is.Not.Null, "warms the cache for this ID");

        _time.Advance(TimeSpan.FromDays(1));

        Assert.That(await _tokens.Authenticate(created.Token), Is.Null, "the cached row's own expiry still applies");
    }

    [Test]
    public async Task ACachedToken_WhoseUserIsGone_SignsNobodyIn()
    {
        var created = await Create(_user);
        await _tokens.Authenticate(created.Token); // warms the cache for this ID
        await _context.Users.Where(u => u.Id == _user.Id).ExecuteDeleteAsync();

        Assert.That(await _tokens.Authenticate(created.Token), Is.Null);
    }

    [Test]
    public async Task Create_DefaultsToMcpOnly_NotReadOnly_AsTokensWereBeforeScopesExisted()
    {
        var created = await Create(_user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.AccessToken.Scope, Is.EqualTo(AccessTokenScope.Mcp));
            Assert.That(created.AccessToken.ReadOnly, Is.False);
        }
    }

    [TestCase(AccessTokenScope.Mcp, false)]
    [TestCase(AccessTokenScope.Api, true)]
    [TestCase(AccessTokenScope.Both, true)]
    public async Task Create_StoresTheChosenScopeAndReadOnlyFlag_AndAuthenticateReturnsThem(AccessTokenScope scope, bool readOnly)
    {
        var (created, refusal) = await _tokens.Create(_user.Id, new CreateAccessTokenRequest("scoped", 30, scope, readOnly));
        Assert.That(refusal, Is.Null);

        var authenticated = await _tokens.Authenticate(created!.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.AccessToken.Scope, Is.EqualTo(scope));
            Assert.That(created.AccessToken.ReadOnly, Is.EqualTo(readOnly));
            Assert.That(authenticated?.Scope, Is.EqualTo(scope));
            Assert.That(authenticated?.ReadOnly, Is.EqualTo(readOnly));
            Assert.That(authenticated?.ID, Is.EqualTo(created.AccessToken.ID));
            Assert.That(authenticated?.Name, Is.EqualTo(created.AccessToken.Name));
        }
    }

    [Test]
    public async Task Create_RefusesAnUndefinedScope()
    {
        // JsonStringEnumConverter accepts an out-of-range integer on the wire; the service must still refuse it
        // rather than store a scope that opens nothing and gives a misleading reason on every use.
        var (created, refusal) = await _tokens.Create(_user.Id, new CreateAccessTokenRequest("bad-scope", 30, (AccessTokenScope)7));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.Null);
            Assert.That(refusal, Does.Contain("Mcp, Api or Both"));
        }
    }

    [Test]
    public async Task ARowStoredBeforeScopesExisted_AuthenticatesAsMcpOnly_NotReadOnly()
    {
        // The JSON a row held before Scope and ReadOnly existed: deserializing it must default them, not throw.
        var created = await Create(_user);
        var row = await _context.UserTokens.SingleAsync(t => t.Name == created.AccessToken.ID);
        var hash = JsonDocument.Parse(row.Value!).RootElement.GetProperty("Hash").GetString();
        row.Value = JsonSerializer.Serialize(new { Hash = hash, Name = "old", CreatedAt = _time.GetUtcNow(), ExpiresAt = _time.GetUtcNow().AddDays(30) });
        await _context.SaveChangesAsync();

        var authenticated = await _tokens.Authenticate(created.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(authenticated, Is.Not.Null);
            Assert.That(authenticated!.Scope, Is.EqualTo(AccessTokenScope.Mcp));
            Assert.That(authenticated.ReadOnly, Is.False);
        }
    }

    [Test]
    public async Task ATokenWhoseUserIsGone_SignsNobodyIn()
    {
        // SQLite doesn't enforce the UserTokens -> Users foreign key (SqliteConnectionSetup turns it off), so a
        // token can outlive its user there; SQL Server and Postgres cascade-delete the row with the user instead.
        // Either way Authenticate must answer null, never throw.
        var created = await Create(_user);
        await _context.Users.Where(u => u.Id == _user.Id).ExecuteDeleteAsync();

        Assert.That(await _tokens.Authenticate(created.Token), Is.Null);
    }

    [Test]
    public async Task List_ShowsTheUsersTokensNewestFirst_WithoutTheirSecrets()
    {
        var older = await Create(_user, "older");
        _time.Advance(TimeSpan.FromMinutes(1));
        var newer = await Create(_user, "newer");
        await Create(_other, "theirs");

        var list = await _tokens.List(_user.Id);

        Assert.That(list, Is.EqualTo(new[] { newer.AccessToken, older.AccessToken }));
    }

    [TestCase(null, 30)]
    [TestCase("", 30)]
    [TestCase("   ", 30)]
    [TestCase("ok", 0)]
    [TestCase("ok", AccessTokenService.MaxDays + 1)]
    public async Task Create_RefusesABadNameOrLifetime(string? name, int days)
    {
        var (created, refusal) = await _tokens.Create(_user.Id, new CreateAccessTokenRequest(name!, days));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.Null);
            Assert.That(refusal, Is.Not.Empty);
        }
    }

    [Test]
    public async Task Create_RefusesMoreThanTheLimit()
    {
        for (var i = 0; i < AccessTokenService.MaxTokensPerUser; i++)
            await Create(_user, $"token {i}");

        var (created, refusal) = await _tokens.Create(_user.Id, new CreateAccessTokenRequest("one more", 30));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.Null);
            Assert.That(refusal, Does.Contain("revoke one first"));
        }
    }
}
