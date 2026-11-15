using System.Globalization;
using System.Security.Claims;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The sweep's check of every connection at once (<see cref="HubSessionValidator"/>), on a temp-file SQLite database: the same answers
/// as Identity's one-by-one check, in two queries.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HubSessionValidatorUnitTests : SqliteGrifballContextFixture
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));

    private HubSessionValidator Validator() => new(null!, Db, Options.Create(new IdentityOptions()), _time);

    /// <summary>A connection's claims: the user and the security stamp its cookie was made with.</summary>
    private static ClaimsPrincipal Claims(int userId, string stamp)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)), new Claim(new IdentityOptions().ClaimsIdentity.SecurityStampClaimType, stamp)],
            "Cookies"));

    [Test]
    public async Task CurrentAsync_EveryConnectionAtOnce_AsIdentityWouldFindThem()
    {
        var staff = await AddUser(5, "Staffer", "Commissioner", "Player");
        var locked = await AddUser(6, "Locked");
        var signedOut = await AddUser(7, "SignedOut");
        staff.SecurityStamp = "staff-stamp";
        locked.SecurityStamp = "locked-stamp";
        locked.LockoutEnabled = true;
        locked.LockoutEnd = _time.GetUtcNow().AddDays(1);
        signedOut.SecurityStamp = "new-stamp";
        await Db.SaveChangesAsync();

        var now = await Validator().CurrentAsync([
            Claims(staff.Id, "staff-stamp"),
            Claims(locked.Id, "locked-stamp"),
            Claims(signedOut.Id, "old-stamp"),
            Claims(999_999, "any"),
            new ClaimsPrincipal(new ClaimsIdentity()),
            Claims(staff.Id, "staff-stamp"),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(now, Has.Count.EqualTo(6));
            Assert.That(now[0]?.SameAs(new HubUserState(staff.Id, ["Player", "Commissioner"])), Is.True, "still them, with their roles");
            Assert.That(now[1], Is.Null, "locked out");
            Assert.That(now[2], Is.Null, "signed out: a new stamp");
            Assert.That(now[3], Is.Null, "no such user");
            Assert.That(now[4], Is.SameAs(HubUserState.Anonymous));
            Assert.That(now[5]?.SameAs(now[0]!), Is.True, "one user's connections alike");
        }
    }

    [Test]
    public async Task CurrentAsync_ALockoutThatHasEnded_IsNoLockout()
    {
        var user = await AddUser(5, "Unlocked");
        user.SecurityStamp = "stamp";
        user.LockoutEnabled = true;
        user.LockoutEnd = _time.GetUtcNow().AddMinutes(-1);
        await Db.SaveChangesAsync();

        var now = await Validator().CurrentAsync([Claims(user.Id, "stamp")]);

        Assert.That(now.Single()?.UserId, Is.EqualTo(user.Id));
    }
}
