using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Mcp;
using Microsoft.EntityFrameworkCore;
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
        _tokens = new AccessTokenService(_context, _time);
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

        var found = await _tokens.FindUser(created.Token);

        Assert.That(found?.Id, Is.EqualTo(_user.Id));
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
            Assert.That(await _tokens.FindUser(wrongSecret), Is.Null, "a wrong secret");
            Assert.That(await _tokens.FindUser(created.Token[AccessTokenService.Prefix.Length..]), Is.Null, "no prefix");
            Assert.That(await _tokens.FindUser(AccessTokenService.Prefix + "short"), Is.Null, "too short to hold an ID");
            Assert.That(await _tokens.FindUser(AccessTokenService.Prefix + new string('f', 32) + "_" + "secret"), Is.Null, "an unknown ID");
        }
    }

    [Test]
    public async Task AToken_StopsWorking_WhenItExpires()
    {
        var created = await Create(_user, days: 1);

        _time.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));
        Assert.That(await _tokens.FindUser(created.Token), Is.Not.Null, "a second before");
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await _tokens.FindUser(created.Token), Is.Null, "when it expires");
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
            Assert.That(await _tokens.FindUser(mine.Token), Is.Null);
            Assert.That(await _tokens.FindUser(theirs.Token), Is.Not.Null);
        }
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
