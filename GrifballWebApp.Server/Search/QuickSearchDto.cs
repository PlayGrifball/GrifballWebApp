namespace GrifballWebApp.Server.Search;

/// <summary>The quick search's hits (the Ctrl+K navigator), a few of each kind, the best first.</summary>
public class QuickSearchDto
{
    public required QuickSearchPlayerDto[] Players { get; set; }
    public required QuickSearchTeamDto[] Teams { get; set; }
    public required QuickSearchSeasonDto[] Seasons { get; set; }
}

/// <summary>A player, linking to their profile.</summary>
public class QuickSearchPlayerDto
{
    public required int UserID { get; set; }
    /// <summary>The name the site shows them by: their gamertag, else their Discord name or display name.</summary>
    public required string Name { get; set; }
    /// <summary>The past gamertag the search matched, when it was one of those and not their current name; else null.</summary>
    public string? PastGamertag { get; set; }
    /// <summary>The Discord or display name the search matched, when that is not the name shown (and no past gamertag
    /// was matched); else null.</summary>
    public string? MatchedName { get; set; }
    /// <summary>Where the name comes from, so two players of one name can be told apart (a gamertag's holder today may
    /// not be who used it in the archive): <see cref="QuickSearchPlayerKind.Xbox"/> when it is the gamertag their Xbox
    /// account holds now, <see cref="QuickSearchPlayerKind.Archive"/> for a player of the imported archive no Xbox account
    /// is linked to; null otherwise (a signed-up player known by a Discord or display name).</summary>
    public QuickSearchPlayerKind? Kind { get; set; }
    /// <summary>An archive player's seasons' years ("2009" or "2009–2011"); null for others, or one with no season.</summary>
    public string? Years { get; set; }
}

/// <summary>Where a player's shown name comes from (sent as its name, like every enum: ApiJson).</summary>
public enum QuickSearchPlayerKind
{
    /// <summary>The name is the gamertag the player's Xbox account holds now.</summary>
    Xbox,
    /// <summary>A player of the imported archive with no Xbox account linked: the name is the one the sheets used.</summary>
    Archive,
}

/// <summary>A team of one season, linking to that season's roster.</summary>
public class QuickSearchTeamDto
{
    public required int TeamID { get; set; }
    public required string TeamName { get; set; }
    public required int SeasonID { get; set; }
    public required string SeasonName { get; set; }
}

/// <summary>A season, linking to its page.</summary>
public class QuickSearchSeasonDto
{
    public required int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public required DateTime Start { get; set; }
    public required DateTime End { get; set; }
    /// <summary>The years it ran in: "2019", or "2018–2019" across years (<see cref="QuickSearchService.YearSpan"/>).</summary>
    public required string Years { get; set; }
}
