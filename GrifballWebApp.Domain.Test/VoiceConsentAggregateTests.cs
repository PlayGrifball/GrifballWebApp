using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>A Discord account's consent to be heard by the bot in voice channels (<see cref="VoiceConsent"/>): its row is the consent.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VoiceConsentAggregateTests
{
    [Test]
    public void Give_KeepsWhoAndWhen()
    {
        var at = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        var consent = VoiceConsent.Give(42, at);

        Assert.That((consent.DiscordUserID, consent.GivenAt), Is.EqualTo((42L, at)));
    }
}
