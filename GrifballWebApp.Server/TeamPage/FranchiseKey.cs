using System.Text.RegularExpressions;
using GrifballWebApp.Server.Historical;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>
/// What makes teams of different seasons one franchise, for the franchise page (<see cref="TeamHistoryService"/>) and the
/// Dynasties board alike: the teams of one league with one name, compared as <see cref="HistoricalKeys.Normalize"/>
/// compares it (trimmed, inner spaces collapsed, any case). A generic placeholder name
/// (<see cref="Franchises.IsPlaceholderName"/>, like "Team 1" in an archived season) names no franchise: such a team is
/// keyed by its own ID, its season's alone.
/// </summary>
/// <param name="Name">The normalized name; null for a placeholder name.</param>
/// <param name="TeamID">The team's own ID, for a placeholder name only.</param>
public readonly record struct FranchiseKey(int LeagueID, string? Name, int? TeamID)
{
    /// <summary>The franchise of the team <paramref name="teamID"/>, of this league and name.</summary>
    public static FranchiseKey Of(int teamID, int leagueID, string? teamName) =>
        Franchises.IsPlaceholderName(teamName)
            ? new FranchiseKey(leagueID, null, teamID)
            : new FranchiseKey(leagueID, HistoricalKeys.Normalize(teamName!), null);
}

/// <summary>The names that make no franchise.</summary>
public static partial class Franchises
{
    /// <summary>
    /// A generic name an archived season gave its teams ("Team 1", "Team #2", "Team A"), or a label that names no team
    /// (<see cref="HistoricalKeys.IsExcludedTeamName"/>): the teams of such a name in different seasons are not one
    /// franchise.
    /// </summary>
    public static bool IsPlaceholderName(string? name) =>
        HistoricalKeys.IsExcludedTeamName(name) || PlaceholderName().IsMatch(HistoricalKeys.CleanGamertag(name!));

    [GeneratedRegex(@"^Team(\s*#?\s*\d+|\s+[A-Z])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderName();
}
