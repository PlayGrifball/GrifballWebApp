using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A password reset link as an aggregate (#352): issued by <see cref="PasswordResetLink.Issue"/>, used once and only before
/// it expires (<see cref="PasswordResetLink.Use"/>), expired early when replaced (<see cref="PasswordResetLink.Expire"/>).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PasswordResetLinkAggregateTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Issue_GivesAUsableLink_KeepingOnlyTheTokensHash()
    {
        var (link, token) = PasswordResetLink.Issue(7, Now);
        var (other, otherToken) = PasswordResetLink.Issue(7, Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(token, Does.Match("^[0-9A-F]{64}$"), "32 random bytes, hex");
            Assert.That(otherToken, Is.Not.EqualTo(token));
            Assert.That(link.Token, Is.EqualTo(PasswordResetLink.HashToken(token)).And.Not.EqualTo(token), "Only the hash is kept");
            Assert.That(link.Token, Has.Length.LessThanOrEqualTo(PasswordResetLink.TokenMaxLength));
            Assert.That(other.Token, Is.Not.EqualTo(link.Token));
            Assert.That(link.UserId, Is.EqualTo(7));
            Assert.That((link.CreatedByID, link.ModifiedByID), Is.EqualTo(((int?)null, (int?)null)), "the audit interceptor stamps its issuer as it saves");
            Assert.That(link.ExpiresAt, Is.EqualTo(Now + PasswordResetLink.Lifetime));
            Assert.That(link.UsedAt, Is.Null);
            Assert.That(link.IsUsableAt(Now), Is.True);
            Assert.That(link.CanUse(Now), Is.EqualTo(PasswordResetLinkUse.Ok));
        }
    }

    [Test]
    public void Use_OnceAndOnlyBeforeItExpires()
    {
        var (link, _) = PasswordResetLink.Issue(7, Now);
        var (late, _) = PasswordResetLink.Issue(7, Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(late.Use(late.ExpiresAt), Is.EqualTo(PasswordResetLinkUse.Expired), "Expired the moment its time is up");
            Assert.That(late.UsedAt, Is.Null, "Refused: unchanged");

            Assert.That(link.Use(Now.AddMinutes(1)), Is.EqualTo(PasswordResetLinkUse.Ok));
            Assert.That(link.UsedAt, Is.EqualTo(Now.AddMinutes(1)));
            Assert.That(link.IsUsableAt(Now.AddMinutes(2)), Is.False);

            Assert.That(link.Use(Now.AddMinutes(2)), Is.EqualTo(PasswordResetLinkUse.AlreadyUsed), "Once");
            Assert.That(link.CanUse(link.ExpiresAt.AddMinutes(1)), Is.EqualTo(PasswordResetLinkUse.AlreadyUsed), "Used, whether expired or not");
            Assert.That(link.UsedAt, Is.EqualTo(Now.AddMinutes(1)), "Its use kept");
        }
    }

    [Test]
    public void Expire_EndsAUsableLinkNow_AndLeavesASpentOneAsItIs()
    {
        var (usable, _) = PasswordResetLink.Issue(7, Now);
        var (used, _) = PasswordResetLink.Issue(7, Now);
        used.Use(Now.AddMinutes(1));
        var (expired, _) = PasswordResetLink.Issue(7, Now);

        usable.Expire(Now.AddMinutes(2));
        used.Expire(Now.AddMinutes(2));
        expired.Expire(Now.AddHours(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(usable.ExpiresAt, Is.EqualTo(Now.AddMinutes(2)));
            Assert.That(usable.CanUse(Now.AddMinutes(2)), Is.EqualTo(PasswordResetLinkUse.Expired));
            Assert.That(usable.UsedAt, Is.Null);
            Assert.That(used.ExpiresAt, Is.EqualTo(Now + PasswordResetLink.Lifetime), "Used: as it was");
            Assert.That(expired.ExpiresAt, Is.EqualTo(Now + PasswordResetLink.Lifetime), "Expired already: as it was");
        }
    }
}
