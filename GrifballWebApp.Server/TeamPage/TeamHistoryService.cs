using GrifballWebApp.Database;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>
/// A franchise's history: a team is a row of one season, so a team that came back season after season (Trophy Hunters,
/// Texas Shock) is the teams of every season of one league with its name. Only one league's: two leagues' teams of a name
/// (or the generic names some archived seasons used, like "Team 1") are not one franchise. Names compare as the database
/// compares text (case-insensitive); a team's name is stored trimmed.
/// </summary>
public class TeamHistoryService
{
    private readonly GrifballContext _context;

    public TeamHistoryService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Every season of the league <paramref name="leagueSlug"/> a team named <paramref name="teamName"/> played, newest
    /// first: its record (counted as the standings count it), roster and captain, and the titles it won (as its team page
    /// shows them); with the franchise's totals. Null when no team of the league has the name.
    /// </summary>
    public async Task<TeamHistoryDto?> GetAsync(string? leagueSlug, string? teamName, CancellationToken ct = default)
    {
        var name = teamName?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(leagueSlug))
            return null;

        var teams = await _context.Teams.AsNoTracking()
            .Where(t => t.TeamName == name && t.Season.League.Slug == leagueSlug)
            // Counted as the standings count it, in the same query
            .Select(TeamRecord.Select(t => new FranchiseTeam
            {
                TeamName = t.TeamName,
                SeasonID = t.SeasonID,
                Titles = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToArray(),
                Players = t.TeamPlayers.AsQueryable().Select(RosterPlayer.Of).ToList(),
            }))
            .AsSplitQuery()
            .ToListAsync(ct);
        if (teams.Count == 0)
            return null;

        var seasons = await SeasonInfoDto.LoadAsync(_context, teams.Select(t => t.SeasonID), ct);
        var lines = teams.Select(t => new TeamHistorySeasonDto
        {
            TeamID = t.TeamID,
            TeamName = t.TeamName,
            Season = seasons[t.SeasonID],
            Wins = t.Wins,
            Losses = t.Losses,
            RecordFromSource = t.FromSource,
            Titles = t.Titles,
            Players = RosterPlayer.ToPlayers(t.Players),
        })
            .OrderByDescending(l => l.Season.Start)
            .ThenByDescending(l => l.Season.SeasonID)
            .ToArray();

        return new TeamHistoryDto
        {
            // The name as the franchise last went by
            TeamName = lines[0].TeamName,
            Appearances = lines.Length,
            Wins = lines.Sum(l => l.Wins),
            Losses = lines.Sum(l => l.Losses),
            Championships = lines.Sum(l => l.Titles.Length),
            Players = lines.SelectMany(l => l.Players).Select(p => p.UserID).Distinct().Count(),
            Seasons = lines,
        };
    }

    /// <summary>One season's team of the franchise, as the database reads it.</summary>
    private sealed class FranchiseTeam : TeamRecord
    {
        public int SeasonID { get; set; }
        public string[] Titles { get; set; } = [];
        public List<RosterPlayer> Players { get; set; } = [];
    }
}

/// <summary>A franchise: every season's team of one name in one league, and its totals.</summary>
public class TeamHistoryDto
{
    /// <summary>The name of its latest season's team.</summary>
    public required string TeamName { get; set; }
    /// <summary>The seasons it played.</summary>
    public int Appearances { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    /// <summary>The titles it won, across seasons and tiers.</summary>
    public int Championships { get; set; }
    /// <summary>The players who played for it, each once.</summary>
    public int Players { get; set; }
    /// <summary>Newest first.</summary>
    public required TeamHistorySeasonDto[] Seasons { get; set; }
}

/// <summary>One season of a franchise: that season's team.</summary>
public class TeamHistorySeasonDto
{
    public required int TeamID { get; set; }
    /// <summary>The name as that season spelled it.</summary>
    public required string TeamName { get; set; }
    public required SeasonInfoDto Season { get; set; }
    public required int Wins { get; set; }
    public required int Losses { get; set; }
    /// <inheritdoc cref="TeamDto.RecordFromSource"/>
    public bool RecordFromSource { get; set; }
    /// <inheritdoc cref="TeamDto.Titles"/>
    public string[] Titles { get; set; } = [];
    /// <summary>Captain first, then by draft round.</summary>
    public required PlayerDto[] Players { get; set; }
}
