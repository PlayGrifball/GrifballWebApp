using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Import;

public partial class HistoricalImportService
{
    /// <summary>
    /// The season's bracket from its bracket sheet (<see cref="ArchiveSeason.Bracket"/>), when it has one: a slot per series
    /// of the sheet, in its bracket, round and number, each side from the slot its team last played (the winner on, the
    /// loser into the losers' bracket), or by its seed for its first: the sheet's, else its standings' rank
    /// (<see cref="Team.ArchivedRank"/>), else the next seed past those. Its results are the sheet's. A bracket the season
    /// already has as the sheet draws it is kept (its games stay linked); any other (an older sheet's, one rebuilt from game
    /// order) goes, its games to be linked again. Returns the slots, for <see cref="LinkSeries"/> to put the season's playoff
    /// games in; null without a bracket sheet (its playoffs are then rebuilt from game order).
    /// </summary>
    private async Task<List<SeasonMatch>?> ImportBracket(SeasonGroup group, Season season, SeasonTeams teams, HistoricalImportReport counts, CancellationToken ct)
    {
        var sheet = group.Entries.Select(e => e.Bracket).FirstOrDefault(b => b is { Series.Count: > 0 });
        if (sheet is null)
            return null;
        var wanted = SheetBracket(group, season, sheet, teams, counts);
        var existing = await _context.SeasonMatches
            .Where(m => m.SeasonID == season.SeasonID && m.BracketMatch != null)
            .Include(m => m.SeriesGames)
            .Include(m => m.BracketMatch!).ThenInclude(b => b.HomeTeamPreviousMatchBracketInfo)
            .Include(m => m.BracketMatch!).ThenInclude(b => b.AwayTeamPreviousMatchBracketInfo)
            .OrderBy(m => m.BracketMatch!.MatchBracketInfoID)
            .ToListAsync(ct);
        // The sheet's slots as they are, beside nothing but series of games no slot of it took (added as played, LinkSeries)
        var drawn = wanted.Select(Signature).ToHashSet(StringComparer.Ordinal);
        var same = existing.Where(m => drawn.Contains(Signature(m))).ToList();
        if (same.Count == wanted.Count && existing.Except(same).All(m => m.SeriesGames.Count > 0))
            return same;
        _context.SeasonMatches.RemoveRange(existing);
        await _context.SaveChangesAsync(ct);
        counts.SeasonMatchesRemoved += existing.Count;
        _context.SeasonMatches.AddRange(wanted);
        await _context.SaveChangesAsync(ct);
        counts.SeasonMatchesCreated += wanted.Count;
        return wanted;
    }

    /// <summary>The sheet's series as the season's bracket slots (not saved). A name that is none of the season's teams
    /// leaves its side without a team, reported once for the season.</summary>
    private List<SeasonMatch> SheetBracket(SeasonGroup group, Season season, ArchiveBracket sheet, SeasonTeams teams, HistoricalImportReport counts)
    {
        var placed = new List<(ArchiveBracketSeries Series, MatchBracketInfo Slot, int? Home, int? Away)>();
        var matches = new List<SeasonMatch>();
        var lastSeed = sheet.Series.SelectMany(s => new[] { s.HomeSeed, s.AwaySeed }).Concat(teams.ByName.Values.Select(t => t.ArchivedRank)).Max() ?? 0;
        int? SeedOf(MatchBracketInfo? from, int? seed, Team? team)
        {
            if (from is not null)
                return null;
            if ((seed ?? team?.ArchivedRank) is int known)
                return known;
            lastSeed++;
            return lastSeed;
        }
        var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var series in sheet.Series)
        {
            var home = SheetTeam(series.Home, teams, unknown);
            var away = SheetTeam(series.Away, teams, unknown);
            if (home is not null && home.TeamID == away?.TeamID)
            {
                Warn(counts, $"{group.SourceId}: the bracket has {home.TeamName} playing itself; that series is left out");
                continue;
            }
            var homeFrom = PlayedBefore(placed, home?.TeamID, series.Home);
            var awayFrom = PlayedBefore(placed, away?.TeamID, series.Away);
            var slot = MatchBracketInfo.From(series.Bracket, series.Round, series.Number,
                SeedOf(homeFrom, series.HomeSeed, home), homeFrom, SeedOf(awayFrom, series.AwaySeed, away), awayFrom);
            var bestOf = series.BestOf ?? SeasonMatch.ImportedBestOf(0, series.HomeWins ?? 0, series.AwayWins ?? 0);
            matches.Add(SeasonMatch.ImportedSlot(season.SeasonID, slot, home?.TeamID, away?.TeamID, series.Winner, series.HomeWins, series.AwayWins, bestOf));
            placed.Add((series, slot, home?.TeamID, away?.TeamID));
        }
        if (unknown.Count > 0)
            Warn(counts, $"{group.SourceId}: the bracket names {unknown.Count} team(s) that are none of the season's ({string.Join(", ", unknown)}); their slots have no team there");
        return matches;
    }

    private static Team? SheetTeam(string name, SeasonTeams teams, SortedSet<string> unknown)
    {
        var team = teams.Find(name);
        if (team is null && !HistoricalKeys.IsExcludedTeamName(name))
            unknown.Add(name.Trim());
        return team;
    }

    /// <summary>The slot a side's team (or, without one, its name) last played before: where it comes from.</summary>
    private static MatchBracketInfo? PlayedBefore(List<(ArchiveBracketSeries Series, MatchBracketInfo Slot, int? Home, int? Away)> placed, int? teamID, string name)
        => placed.LastOrDefault(p => teamID is not null
            ? p.Home == teamID || p.Away == teamID
            : string.Equals(p.Series.Home, name, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Series.Away, name, StringComparison.OrdinalIgnoreCase)).Slot;

    /// <summary>A bracket slot as text (where it is, who plays, where they come from, its result): two slots with the same
    /// text are the same.</summary>
    private static string Signature(SeasonMatch match)
    {
        var slot = match.BracketMatch!;
        return string.Join('|', slot.Bracket, slot.RoundNumber, slot.MatchNumber, match.HomeTeamID, match.AwayTeamID,
            slot.HomeTeamSeedNumber, slot.AwayTeamSeedNumber, SlotKey(slot.HomeTeamPreviousMatchBracketInfo), SlotKey(slot.AwayTeamPreviousMatchBracketInfo),
            match.HomeTeamResult, match.AwayTeamResult, match.HomeTeamScore, match.AwayTeamScore, match.BestOf);
    }

    private static string SlotKey(MatchBracketInfo? slot) => slot is null ? "" : $"{slot.Bracket}{slot.RoundNumber}.{slot.MatchNumber}";

    /// <summary>The sheet's slot a playoff series of these teams is: the first of theirs without games yet, else their last
    /// (a series split over days); null without a bracket sheet, or when the sheet has none of theirs.</summary>
    private static SeasonMatch? SheetSlotOf(List<SeasonMatch>? bracket, int? home, int? away)
    {
        if (bracket is null || home is null || away is null)
            return null;
        var theirs = bracket.Where(m => (m.HomeTeamID == home && m.AwayTeamID == away) || (m.HomeTeamID == away && m.AwayTeamID == home)).ToList();
        return theirs.Find(m => m.SeriesGames.Count == 0) ?? theirs.LastOrDefault();
    }
}
