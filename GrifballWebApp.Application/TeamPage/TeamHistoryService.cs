using GrifballWebApp.Application.Database;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.TeamPage;

/// <summary>
/// A franchise's history: a team is a row of one season, so a team that came back season after season (Trophy Hunters,
/// Texas Shock) is the teams of every season of one league with its name. Only one league's: two leagues' teams of a name
/// are not one franchise. Teams are one franchise by <see cref="FranchiseKey"/>, as the Dynasties board groups them: names
/// compare trimmed and case-insensitively, and a generic placeholder name (like "Team 1" in an archived season) names no
/// franchise, so such a team's history is its own season alone.
/// </summary>
public class TeamHistoryService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    private readonly SeasonVisibility _visibility;

    public TeamHistoryService(IApplicationDbContext context, SeasonVisibility visibility, TimeProvider? time = null)
    {
        _visibility = visibility;
        _time = time ?? TimeProvider.System;
        _context = context;
    }

    /// <summary>
    /// The franchise of the team <paramref name="teamID"/>: every season of its league a team of its name played, newest
    /// first, with its record (counted as the standings count it), roster and captain, and the titles it won (as its
    /// team page shows them); with the franchise's totals: of the seasons <paramref name="viewer"/> may see, so a season not
    /// announced yet stays out of a public franchise's history (#435). Null when there is no such team.
    /// </summary>
    public async Task<TeamHistoryDto?> GetAsync(int teamID, SeasonViewer viewer = SeasonViewer.Public, CancellationToken ct = default)
    {
        var team = await _context.Teams.AsNoTracking()
            .Where(t => t.TeamID == teamID)
            .Select(t => new { t.TeamName, t.Season.LeagueID, t.Season.League.LeagueName })
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;

        var leagueID = team.LeagueID;
        var key = FranchiseKey.Of(teamID, leagueID, team.TeamName);
        // Keyed in memory, as the Dynasties board keys them (names stored untrimmed before signups trimmed them still
        // match): a league's teams are few
        List<int> ids = key.TeamID is int own ? [own] : (await _context.Teams.AsNoTracking()
            .Where(t => t.Season.LeagueID == leagueID)
            .Select(t => new { t.TeamID, t.TeamName })
            .ToListAsync(ct))
            .Where(t => FranchiseKey.Of(t.TeamID, leagueID, t.TeamName) == key)
            .Select(t => t.TeamID)
            .ToList();
        var visible = _visibility.Visible(viewer);
        var teams = await _context.Teams.AsNoTracking()
            .Where(t => ids.Contains(t.TeamID) && visible.Any(s => s.SeasonID == t.SeasonID))
            // Counted as the standings count it, in the same query
            .Select(TeamRecord.Select(t => new FranchiseTeam
            {
                SeasonID = t.SeasonID,
                Titles = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToArray(),
                Players = t.TeamPlayers.AsQueryable().Select(RosterPlayer.Of).ToList(),
            }))
            .AsSplitQuery()
            .ToListAsync(ct);

        var seasons = await SeasonInfoDto.LoadAsync(_context, teams.Select(t => t.SeasonID), _time.GetUtcNow().UtcDateTime, ct);
        var lines = teams.Select(t => new TeamHistorySeasonDto
        {
            TeamID = t.TeamID,
            TeamName = t.TeamName,
            Season = seasons[t.SeasonID],
            Wins = t.Wins,
            Losses = t.Losses,
            RecordFromSource = t.FromSource,
            // In one order on every load: the database returns distinct values in none
            Titles = t.Titles.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Players = RosterPlayer.ToPlayers(t.Players),
        })
            .OrderByDescending(l => l.Season.Start)
            .ThenByDescending(l => l.Season.SeasonID)
            .ToArray();

        // None the viewer may see (a team of a season not announced yet, alone in its franchise): no such team to them
        if (lines.Length == 0)
            return null;

        return new TeamHistoryDto
        {
            // The name as the franchise last went by
            TeamName = lines[0].TeamName,
            LeagueName = team.LeagueName,
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
    /// <summary>The league it played in.</summary>
    public required string LeagueName { get; set; }
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
