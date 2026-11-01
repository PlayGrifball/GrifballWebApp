namespace GrifballWebApp.Server.Historical.Vods;

public class VodTeamDto
{
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
}

/// <summary>A VOD on a season, team, match or player page.</summary>
public class VodDto
{
    public int VodID { get; set; }
    public required string YouTubeVideoID { get; set; }
    public required string Title { get; set; }
    public DateTime PublishedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public int? SeasonID { get; set; }
    public string? SeasonName { get; set; }
    public int? SeasonMatchID { get; set; }
    public List<VodTeamDto> Teams { get; set; } = [];
}

/// <summary>A VOD waiting for a sysadmin, with the matcher's suggestion.</summary>
public class VodReviewDto : VodDto
{
    public string? Description { get; set; }
    public bool PublishedAtIsApproximate { get; set; }
    public required string MatchStatus { get; set; }
    public string? MatchNote { get; set; }
}

/// <summary>What a VOD is of: a season match (its season and teams follow from it), or a season and its teams.</summary>
public class LinkVodRequest
{
    public int? SeasonID { get; set; }
    public int? SeasonMatchID { get; set; }
    public List<int> TeamIDs { get; set; } = [];
}

public class VodLinkSeasonDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
}

public class VodLinkMatchDto
{
    public int SeasonMatchID { get; set; }
    /// <summary>"Home vs Away · Oct 3, 2026".</summary>
    public required string Label { get; set; }
    public List<int> TeamIDs { get; set; } = [];
}

/// <summary>A season's teams and matches, to link a VOD to.</summary>
public class VodLinkOptionsDto
{
    public List<VodTeamDto> Teams { get; set; } = [];
    public List<VodLinkMatchDto> Matches { get; set; } = [];
}
