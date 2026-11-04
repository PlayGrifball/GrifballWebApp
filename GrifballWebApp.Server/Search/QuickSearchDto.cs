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
}
