namespace GrifballWebApp.Domain.Models;

/// <summary>
/// A Discord account, as Discord gave it: its ID and its username (<see cref="FromDiscord"/>, renamed only by
/// <see cref="Rename"/>). Its user links to it from their side (<see cref="Models.User.LinkDiscord(DiscordUser)"/>); the bot's
/// messages to and from it (<see cref="DiscordMessage"/>) are records of their own.
/// </summary>
[AggregateRoot]
public class DiscordUser : AuditableEntity
{
    /// <summary>For EF. An account begins as Discord gives it: <see cref="FromDiscord"/>.</summary>
    private DiscordUser() { }

    /// <summary>
    /// The account Discord gave: its ID and its username. With no username (none given, or blank) it goes by its ID, so it
    /// always has a name to show. Spaces around it are trimmed.
    /// </summary>
    public static DiscordUser FromDiscord(long discordUserID, string? username)
        => new() { DiscordUserID = discordUserID, DiscordUsername = string.IsNullOrWhiteSpace(username) ? discordUserID.ToString() : username.Trim() };

    /// <summary>A username that may be stored: not blank (CK_User_Discord_UsernameNotBlank).</summary>
    public static bool IsUsername(string? username) => !string.IsNullOrWhiteSpace(username);

    // The ID is Discord's (never generated); internal so tests may give one. Nothing outside the account sets its username,
    // its user or its messages: only its methods, and EF (which uses the private setters and fields).
    public long DiscordUserID { get; internal set; }
    /// <summary>Its username, as Discord last gave it: never blank.</summary>
    public string DiscordUsername { get; private set; } = null!;

    /// <summary>The user linked to it (<see cref="Models.User.LinkDiscord(DiscordUser)"/>): the user's foreign key, set from their side.</summary>
    public User? User { get; private set; }
    // The bot's messages to and from it: records of their own (DiscordMessage), so read-only here (EF uses the fields)
    public IReadOnlyCollection<DiscordMessage> ReceivedMessages => _receivedMessages;
    private readonly List<DiscordMessage> _receivedMessages = [];
    public IReadOnlyCollection<DiscordMessage> SentMessages => _sentMessages;
    private readonly List<DiscordMessage> _sentMessages = [];

    /// <summary>
    /// Discord gave the account a username, <paramref name="username"/> (a sign-in's name claim, say):
    /// <see cref="DiscordRename.Renamed"/> when it differs from the one stored, <see cref="DiscordRename.Unchanged"/> when
    /// it is the same (spaces around it trimmed). No username (none given, or blank) is <see cref="DiscordRename.NoUsername"/>, and the account keeps its own.
    /// </summary>
    public DiscordRename Rename(string? username)
    {
        if (!IsUsername(username))
            return DiscordRename.NoUsername;
        var trimmed = username!.Trim();
        if (DiscordUsername == trimmed)
            return DiscordRename.Unchanged;
        DiscordUsername = trimmed;
        return DiscordRename.Renamed;
    }
}

/// <summary>What <see cref="DiscordUser.Rename"/> did.</summary>
public enum DiscordRename
{
    Renamed = 1,
    /// <summary>The account has that username already: nothing changed.</summary>
    Unchanged = 2,
    /// <summary>No username was given: the account keeps its own.</summary>
    NoUsername = 3,
}
