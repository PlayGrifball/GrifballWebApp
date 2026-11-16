using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Players;

/// <summary>
/// A Discord account's consent to be heard by the bot in voice channels (Discord's /voice optin): its row is the consent, removed
/// when they opt out. Anyone without one is never transcribed. No foreign key: an account the bot hasn't seen may opt in.
/// </summary>
[AggregateRoot]
public class VoiceConsent
{
    /// <summary>For EF. A consent begins through <see cref="Give"/>.</summary>
    private VoiceConsent() { }

    /// <summary><paramref name="discordUserId"/> consented at <paramref name="at"/> (UTC).</summary>
    public static VoiceConsent Give(long discordUserId, DateTime at) => new() { DiscordUserID = discordUserId, GivenAt = at };

    public long DiscordUserID { get; private set; }

    /// <summary>When they opted in (UTC).</summary>
    public DateTime GivenAt { get; private set; }
}
