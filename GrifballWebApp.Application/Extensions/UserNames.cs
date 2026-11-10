using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Extensions;

/// <summary>
/// The name the app shows for a user: the Xbox gamertag, else the Discord name, else the display name. Never a user's
/// user name, which is their login name and private; a placeholder of the historical import, whose user name is internal
/// ("historical:..."), goes by the gamertag in it.
/// </summary>
public static class UserNames
{
    public static string Pick(string? gamertag, string? discordName, string? displayName, string? userName)
        => gamertag ?? discordName ?? displayName ?? PlaceholderName(userName) ?? "";

    /// <summary>The gamertag a placeholder of the historical import is named after. <see cref="User.PlaceholderGamertagOf"/>.</summary>
    public static string? PlaceholderName(string? userName) => User.PlaceholderGamertagOf(userName);
}
