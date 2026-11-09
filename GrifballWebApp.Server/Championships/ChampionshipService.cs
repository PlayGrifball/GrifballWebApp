using GrifballWebApp.Database.Queries;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Championships;

/// <summary>
/// Championships (<see cref="PlayerChampionship"/>): one row per player on the winning roster of a season's tier, recorded by
/// the season (<see cref="Season.RecordChampions"/>). A season of the app's gets its title when its playoff bracket is decided
/// (<see cref="AwardFromBracketAsync"/>, after every reported, forfeited or undone result); the historical import records
/// the archive's titles itself (<see cref="Season.RecordArchivedChampions"/>).
/// </summary>
public class ChampionshipService
{
    /// <summary>The tier of a season's title when the season has no division: the app's seasons are one open tier.</summary>
    public const string DefaultTier = Season.DefaultChampionTier;

    private readonly GrifballContext _context;

    public ChampionshipService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Makes <paramref name="teamID"/> the champions of the season's tier (its division, <see cref="DefaultTier"/> without
    /// one): a title for each player on the team's roster, its captain flagged, and the tier's titles of anyone else (a
    /// champion a corrected result replaced) go. A null team takes the tier's titles away. Idempotent: running it again
    /// changes nothing. An imported season keeps its record's titles: null, nothing changed. Saves.
    /// </summary>
    public async Task<ChampionshipChanges?> RecordChampionsAsync(int seasonID, int? teamID, CancellationToken ct = default)
    {
        var season = await _context.Seasons.WithChampionships().SingleAsync(s => s.SeasonID == seasonID, ct);
        if (season.IsImported)
            return null;
        var team = teamID is int id ? await _context.Teams.SingleAsync(t => t.TeamID == id, ct) : null;
        var roster = teamID is int rosterOf ? await RosterAsync(rosterOf, ct) : [];
        var changes = season.RecordChampions(team, roster);
        await _context.SaveChangesAsync(ct);
        return changes;
    }

    /// <summary>
    /// Awards the season's title from its playoff bracket: the bracket's champion, once it is decided, gets the title of
    /// the season's division (<see cref="DefaultTier"/> without one); an undecided bracket (an undone final, a bracket
    /// made again or reseeded, or none any more) has none. A season imported from the archive keeps its record's titles
    /// (its bracket is rebuilt from game order and decides nothing). Reads what is saved. Returns what changed.
    /// </summary>
    public async Task<ChampionshipChanges?> AwardFromBracketAsync(int seasonID, CancellationToken ct = default)
    {
        // An imported season's titles are its champions record's: RecordChampionsAsync (Season.RecordChampions) keeps them
        var bracket = await _context.SeasonMatches.AsNoTracking()
            .Include(m => m.BracketMatch)
            .Where(m => m.SeasonID == seasonID && m.BracketMatch != null)
            .ToListAsync(ct);
        return await RecordChampionsAsync(seasonID, ChampionOf(bracket), ct);
    }

    /// <summary>
    /// The team that won the bracket, or null while it is undecided. Double elimination: the grand final's winner when
    /// the winner bracket's champion (the home team) wins it or it was decided by a BYE, otherwise the sudden death's.
    /// Single elimination: the winner of the last round's match. The season's bracket matches, with their slots.
    /// </summary>
    public static int? ChampionOf(IReadOnlyCollection<SeasonMatch> bracket)
    {
        var grandFinal = bracket.FirstOrDefault(m => m.BracketMatch!.Bracket is Bracket.GrandFinal);
        if (grandFinal is not null)
        {
            var winner = grandFinal.WinnerTeamID;
            if (winner is null)
                return null;
            var suddenDeath = bracket.FirstOrDefault(m => m.BracketMatch!.Bracket is Bracket.GrandFinalSuddenDeath);
            if (grandFinal.Winner is SeasonMatchSide.Home || grandFinal.DecidedBy is SeasonMatchDecision.Bye || suddenDeath is null)
                return winner;
            return suddenDeath.WinnerTeamID;
        }

        var final = bracket.Where(m => m.BracketMatch!.Bracket is Bracket.Winner).MaxBy(m => m.BracketMatch!.RoundNumber);
        return final?.WinnerTeamID;
    }

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
                    Gamertag = tp.User.XboxUser != null ? tp.User.XboxUser.Gamertag : null,
                    Discord = tp.User.DiscordUser != null ? tp.User.DiscordUser.DiscordUsername : null,
                    tp.User.DisplayName,
                    tp.User.UserName,
                }).ToList(),
            })
            .FirstAsync(ct);
        return team.Players
            .OrderBy(p => p.TeamPlayerID)
            .Select(p => new ChampionMember(p.UserID, UserNames.Pick(p.Gamertag, p.Discord, p.DisplayName, p.UserName), p.TeamPlayerID == team.CaptainID))
            .ToList();
    }
}
