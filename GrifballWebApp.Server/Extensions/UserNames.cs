using GrifballWebApp.Server.Historical;

namespace GrifballWebApp.Server.Extensions;

/// <summary>
/// The name the app shows for a user, as the rest of the app writes it inline in its queries (TeamService, SignupsService,
/// MatchPlannerService): the Xbox gamertag, else the Discord name, else the display name, else the user name. A
/// placeholder of the historical import is never shown by its internal user name ("historical:..."): by the gamertag in it.
/// </summary>
public static class UserNames
{
    public static string Pick(string? gamertag, string? discordName, string? displayName, string? userName)
        => gamertag ?? discordName ?? displayName ?? WithoutPlaceholderPrefix(userName) ?? "";

    public static string? WithoutPlaceholderPrefix(string? userName)
        => userName is not null && userName.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.Ordinal)
            ? userName[HistoricalKeys.DummyUserNamePrefix.Length..]
            : userName;
}
