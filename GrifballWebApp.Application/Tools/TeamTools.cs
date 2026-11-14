using System.ComponentModel;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.TeamPage;

namespace GrifballWebApp.Application.Tools;

/// <summary>Teams: what a team's page shows, open to everyone on the site. A team is one season's; search finds its ID.</summary>
public class TeamTools
{
    private readonly TeamPageService _teams;
    private readonly TeamHistoryService _history;
    private readonly VodService _vods;

    public TeamTools(TeamPageService teams, TeamHistoryService history, VodService vods)
    {
        _teams = teams;
        _history = history;
        _vods = vods;
    }

    private static ToolException NoSuchTeam(int teamID) => new($"There is no team {teamID}: search finds teams by name.");

    [Description("A team: its season, record, titles and roster (each player's ID, gamertag, whether they captain and their draft round).")]
    public async Task<TeamDto> GetTeam([Description("The team's ID.")] int teamID, CancellationToken ct = default)
        => await _teams.GetAsync(teamID, ct) ?? throw NoSuchTeam(teamID);

    [Description("A team's matches in its season, soonest unplayed first: each one's ID, opponent, time, best-of, score and result.")]
    public async Task<MatchDto[]> GetTeamMatches([Description("The team's ID.")] int teamID, CancellationToken ct = default)
        => await _teams.GetAsync(teamID, ct) is null ? throw NoSuchTeam(teamID) : await _teams.GetMatchesAsync(teamID, ct);

    [Description("A franchise's history: every season's team of that name in its league, with records, titles and totals.")]
    public async Task<TeamHistoryDto> GetTeamHistory([Description("The team's ID (any season's).")] int teamID, CancellationToken ct = default)
        => await _history.GetAsync(teamID, ct) ?? throw NoSuchTeam(teamID);

    [Description("Recorded videos (VODs) of a team's matches.")]
    public async Task<List<VodDto>> GetTeamVods([Description("The team's ID.")] int teamID, CancellationToken ct = default)
        => await _teams.GetAsync(teamID, ct) is null ? throw NoSuchTeam(teamID) : await _vods.ForTeam(teamID, ct);
}
