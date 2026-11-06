using System.Text.RegularExpressions;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>
/// A franchise's history: a team is a row of one season, so a team that came back season after season (Trophy Hunters,
/// Texas Shock) is the teams of every season of one league with its name. Only one league's: two leagues' teams of a name
/// are not one franchise. Names compare trimmed and case-insensitively (the database compares text so on every
/// provider). A generic placeholder name (<see cref="IsPlaceholderName"/>, like "Team 1" in an archived season) names no
/// franchise: such a team's history is its own season alone.
/// </summary>
public partial class TeamHistoryService
{
    private readonly GrifballContext _context;

    public TeamHistoryService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The franchise of the team <paramref name="teamID"/>: every season of its league a team of its name played, newest
    /// first, with its record (counted as the standings count it), roster and captain, and the titles it won (as its
    /// team page shows them); with the franchise's totals. Null when there is no such team.
    /// </summary>
    public async Task<TeamHistoryDto?> GetAsync(int teamID, CancellationToken ct = default)
    {
        var team = await _context.Teams.AsNoTracking()
            .Where(t => t.TeamID == teamID)
            .Select(t => new { t.TeamName, t.Season.LeagueID, t.Season.League.LeagueName })
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;

        var name = team.TeamName.Trim();
        var leagueID = team.LeagueID;
        var franchise = IsPlaceholderName(name)
            ? _context.Teams.Where(t => t.TeamID == teamID)
            // Both sides trimmed: names stored before signups trimmed them still match
            : _context.Teams.Where(t => t.Season.LeagueID == leagueID && t.TeamName.Trim() == name);
        var teams = await franchise.AsNoTracking()
            // Counted as the standings count it, in the same query
            .Select(TeamRecord.Select(t => new FranchiseTeam
            {
                SeasonID = t.SeasonID,
                Titles = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToArray(),
                Players = t.TeamPlayers.AsQueryable().Select(RosterPlayer.Of).ToList(),
            }))
            .AsSplitQuery()
            .ToListAsync(ct);

        var seasons = await SeasonInfoDto.LoadAsync(_context, teams.Select(t => t.SeasonID), ct);
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

    /// <summary>
    /// A generic name an archived season gave its teams ("Team 1", "Team #2", "Team A"), or a label that names no team
    /// (<see cref="HistoricalKeys.IsExcludedTeamName"/>): the teams of such a name in different seasons are not one
    /// franchise.
    /// </summary>
    public static bool IsPlaceholderName(string? name) =>
        HistoricalKeys.IsExcludedTeamName(name) || PlaceholderName().IsMatch(HistoricalKeys.CleanGamertag(name!));

    [GeneratedRegex(@"^Team(\s*#?\s*\d+|\s+[A-Z])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderName();

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
