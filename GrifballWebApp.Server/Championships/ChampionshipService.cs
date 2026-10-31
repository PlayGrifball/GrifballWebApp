using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Championships;

/// <summary>A player on a championship-winning roster, as the title records them.</summary>
public record ChampionMember(int UserID, string Gamertag, bool IsCaptain);

/// <summary>What <see cref="ChampionshipService.SetChampionsAsync"/> changed.</summary>
public record ChampionshipChanges(int Inserted, int Updated, int Removed);

/// <summary>
/// Championships (<see cref="PlayerChampionship"/>): one row per player on the winning roster of a season's tier. Both
/// ways a title comes in write them here: a season's playoff bracket being decided in the app
/// (<see cref="AwardFromBracketAsync"/>, after every reported, forfeited or undone result) and the historical import.
/// </summary>
public class ChampionshipService
{
    /// <summary>The tier of a season's title when the season has no division: the app's seasons are one open tier.</summary>
    public const string DefaultTier = "Open";

    private readonly GrifballContext _context;

    public ChampionshipService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Makes <paramref name="teamID"/> the champions of the season's <paramref name="tier"/>: a title for each of
    /// <paramref name="members"/> (the team's roster when null, its captain flagged), and the tier's titles of anyone else
    /// (a champion a corrected result replaced) go. A null team takes the tier's titles away. Idempotent: running it again
    /// changes nothing. Saves.
    /// </summary>
    public async Task<ChampionshipChanges> SetChampionsAsync(int seasonID, string tier, int? teamID, IReadOnlyCollection<ChampionMember>? members, int? gameVersionID, CancellationToken ct = default)
    {
        tier = tier.Trim();
        var leagueID = await _context.Seasons.Where(s => s.SeasonID == seasonID).Select(s => s.LeagueID).FirstAsync(ct);
        var existing = await _context.PlayerChampionships.Where(c => c.SeasonID == seasonID && c.Tier == tier).ToListAsync(ct);
        // The tier as its titles already spell it (tiers compare case-insensitively)
        tier = existing.FirstOrDefault()?.Tier ?? tier;

        int inserted = 0, updated = 0;
        var kept = new HashSet<int>();
        if (teamID is not null)
        {
            members ??= await RosterAsync(teamID.Value, ct);
            foreach (var member in members)
            {
                if (!kept.Add(member.UserID))
                    continue;
                var row = existing.FirstOrDefault(c => c.UserID == member.UserID);
                if (row is null)
                {
                    row = new PlayerChampionship { SeasonID = seasonID, Tier = tier, UserID = member.UserID };
                    _context.PlayerChampionships.Add(row);
                    inserted++;
                }
                else
                {
                    updated++;
                }
                row.TeamID = teamID.Value;
                row.LeagueID = leagueID;
                row.GameVersionID = gameVersionID;
                row.IsCaptain = member.IsCaptain;
                row.GamertagUsed = member.Gamertag.Length > 50 ? member.Gamertag[..50] : member.Gamertag;
            }
        }

        var stale = existing.Where(c => !kept.Contains(c.UserID)).ToList();
        _context.PlayerChampionships.RemoveRange(stale);
        await _context.SaveChangesAsync(ct);
        return new ChampionshipChanges(inserted, updated, stale.Count);
    }

    /// <summary>
    /// Awards the season's title from its playoff bracket: the bracket's champion, once it is decided, gets the title of
    /// the season's division (<see cref="DefaultTier"/> without one), and an undecided bracket (an undone final) has none.
    /// Does nothing for a season without a bracket. Reads what is saved. Returns what changed, null without a bracket.
    /// </summary>
    public async Task<ChampionshipChanges?> AwardFromBracketAsync(int seasonID, CancellationToken ct = default)
    {
        var bracket = await _context.SeasonMatches.AsNoTracking()
            .Where(m => m.SeasonID == seasonID && m.BracketMatch != null)
            .Select(m => new BracketResult(m.BracketMatch!.Bracket, m.BracketMatch.RoundNumber, m.HomeTeamID, m.HomeTeamResult, m.AwayTeamID, m.AwayTeamResult))
            .ToListAsync(ct);
        if (bracket.Count == 0)
            return null;

        var season = await _context.Seasons.AsNoTracking().Where(s => s.SeasonID == seasonID)
            .Select(s => new { s.Division, s.GameVersionID }).FirstAsync(ct);
        var tier = string.IsNullOrWhiteSpace(season.Division) ? DefaultTier : season.Division;
        return await SetChampionsAsync(seasonID, tier, ChampionOf(bracket), null, season.GameVersionID, ct);
    }

    /// <summary>One bracket match's teams and results.</summary>
    public record BracketResult(Bracket Bracket, int RoundNumber, int? HomeTeamID, SeasonMatchResult? HomeTeamResult, int? AwayTeamID, SeasonMatchResult? AwayTeamResult);

    /// <summary>
    /// The team that won the bracket, or null while it is undecided. Double elimination: the grand final's winner when
    /// the winner bracket's champion (the home team) wins it or it was decided by a BYE, otherwise the sudden death's.
    /// Single elimination: the winner of the last round's match.
    /// </summary>
    public static int? ChampionOf(IReadOnlyCollection<BracketResult> bracket)
    {
        var grandFinal = bracket.FirstOrDefault(m => m.Bracket is Bracket.GrandFinal);
        if (grandFinal is not null)
        {
            var winner = WinnerOf(grandFinal);
            if (winner is null)
                return null;
            var suddenDeath = bracket.FirstOrDefault(m => m.Bracket is Bracket.GrandFinalSuddenDeath);
            if (grandFinal.HomeTeamResult is SeasonMatchResult.Won || grandFinal.HomeTeamID is null || suddenDeath is null)
                return winner;
            return WinnerOf(suddenDeath);
        }

        var final = bracket.Where(m => m.Bracket is Bracket.Winner).MaxBy(m => m.RoundNumber);
        return final is null ? null : WinnerOf(final);
    }

    private static int? WinnerOf(BracketResult match) => match switch
    {
        { HomeTeamResult: SeasonMatchResult.Won } => match.HomeTeamID,
        { AwayTeamResult: SeasonMatchResult.Won } => match.AwayTeamID,
        _ => null,
    };

    private async Task<List<ChampionMember>> RosterAsync(int teamID, CancellationToken ct)
    {
        var team = await _context.Teams.AsNoTracking().Where(t => t.TeamID == teamID)
            .Select(t => new
            {
                t.CaptainID,
                Players = t.TeamPlayers.Select(tp => new
                {
                    tp.TeamPlayerID,
                    tp.UserID,
                    Gamertag = tp.User.XboxUser != null ? tp.User.XboxUser.Gamertag : tp.User.DisplayName ?? tp.User.UserName,
                }).ToList(),
            })
            .FirstAsync(ct);
        return team.Players
            .OrderBy(p => p.TeamPlayerID)
            .Select(p => new ChampionMember(p.UserID, p.Gamertag ?? "", p.TeamPlayerID == team.CaptainID))
            .ToList();
    }
}
