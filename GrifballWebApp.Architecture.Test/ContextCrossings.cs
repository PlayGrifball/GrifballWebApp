namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// The Domain's bounded contexts, and every crossing between them that BoundedContextTests lets through. The owner keeps
/// the navigations between contexts (a Season's teams' players are Users, a RecordedGame names its Season): this list is
/// what they are today, so a new one is a decision made in review, not an accident.
/// </summary>
internal static class ContextCrossings
{
    /// <summary>The Domain's namespaces under GrifballWebApp.Domain, one per bounded context (Shared: the shared kernel).</summary>
    public static readonly string[] Contexts = ["Archive", "Halo", "LateLeague", "League", "Players", "Shared"];

    /// <summary>
    /// Which types of another context a context's types may name: from-context -> to-context -> the types' names.
    /// </summary>
    /// <remarks>
    /// To add a crossing: when AContext_NamesAnotherContextsType_OnlyWhereAllowed fails, it lists each new one as
    /// "From -> To: Type (in the type that names it)". Ask whether it belongs (could the code go through a type of its own
    /// context, or the Shared kernel's?); if it does, add Type to Allowed[From][To] below (a new [To] or [From] entry if
    /// there is none), keeping the names in order. A crossing the code no longer makes comes off the list
    /// (EveryAllowedCrossing_IsStillMade says which).
    /// </remarks>
    public static readonly Dictionary<string, Dictionary<string, string[]>> Allowed = new()
    {
        ["Archive"] = new()
        {
            ["Halo"] = ["Outcomes"],
            ["LateLeague"] = ["MatchedMatch"],
            ["League"] = ["Season", "SeriesGame", "Team"],
            ["Players"] = ["User", "XboxUser"],
            ["Shared"] = ["AggregatePartAttribute", "AggregateRootAttribute", "AuditableEntity", "GameVersion"],
        },
        ["Halo"] = new()
        {
            ["LateLeague"] = ["MatchedMatch"],
            ["League"] = ["SeriesGame"],
            ["Players"] = ["XboxUser"],
            ["Shared"] = ["AggregatePartAttribute", "AggregateRootAttribute", "AuditableEntity", "ModelRules"],
        },
        ["LateLeague"] = new()
        {
            ["Archive"] = ["RecordedGame"],
            ["Halo"] = ["Match"],
            ["League"] = ["PlayerSeasonStatConfiguration"],
            ["Players"] = ["User"],
            ["Shared"] = ["AggregateAccessAttribute", "AggregatePartAttribute", "AggregateRootAttribute",
                "AuditableEntity", "GameVersion", "PlayerStatTotals", "StatSheetTotals"],
        },
        ["League"] = new()
        {
            ["Archive"] = ["RecordedGame"],
            ["Halo"] = ["Match"],
            ["Players"] = ["User"],
            ["Shared"] = ["AggregateAccessAttribute", "AggregatePartAttribute", "AggregateRootAttribute",
                "AuditableEntity", "AvailabilityOption", "GameVersion", "League", "ModelRules", "PlayerStatTotals", "StatSheetTotals"],
        },
        ["Players"] = new()
        {
            ["Halo"] = ["MatchParticipant"],
            ["LateLeague"] = ["MatchedPlayer", "QueuedPlayer"],
            ["League"] = ["SeasonSignup", "TeamPlayer"],
            ["Shared"] = ["AggregatePartAttribute", "AggregateRootAttribute",
                "AuditableEntity", "DiscordMessage", "GameVersion", "IAuditable", "IAuditStamp", "Region", "RegionSource"],
        },
        ["Shared"] = new()
        {
            ["League"] = ["Season", "SeasonAvailability", "TeamAvailability"],
            ["Players"] = ["DiscordUser", "User", "UserExperience"],
        },
    };

    /// <summary>Whether a type of <paramref name="from"/> may name <paramref name="to"/>'s type <paramref name="type"/>.</summary>
    public static bool Allows(string from, string to, string type)
        => from == to || (Allowed.TryGetValue(from, out var targets) && targets.TryGetValue(to, out var types) && types.Contains(type));
}
