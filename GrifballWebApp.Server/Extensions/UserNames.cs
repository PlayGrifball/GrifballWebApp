using GrifballWebApp.Server.Historical;

namespace GrifballWebApp.Server.Extensions;

/// <summary>
/// The name the app shows for a user: the Xbox gamertag, else the Discord name, else the display name. Never a user's
/// user name, which is their login name and private; a placeholder of the historical import, whose user name is internal
/// ("historical:..."), goes by the gamertag in it.
/// </summary>
public static class UserNames
{
    public static string Pick(string? gamertag, string? discordName, string? displayName, string? userName)
        => gamertag ?? discordName ?? displayName ?? PlaceholderName(userName) ?? "";

    /// <summary>The gamertag a placeholder of the historical import is named after ("historical:Name" is "Name"); null for
    /// anyone else, whose user name is their login name, never shown.</summary>
    public static string? PlaceholderName(string? userName)
        => userName is not null && userName.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.Ordinal)
            ? userName[HistoricalKeys.DummyUserNamePrefix.Length..]
            : null;
}
