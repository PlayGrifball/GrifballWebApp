using System.Text.RegularExpressions;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;

namespace GrifballWebApp.Historical.Parsing;

public sealed partial class HistoricalArchiveBuilder
{
    /// <summary>
    /// Each schedule's matches date their season's undated games. The schedule's matches of the same two teams on one day are
    /// one meeting; in the schedule's order, a meeting dates the first undated series of those teams (in their result list's
    /// order), and the next, until it has dated as many games as it has entries (a series' games each an entry, a per-game
    /// tab's games each their own). A meeting with nothing left to date is left out, and said in a warning; of a season whose
    /// results are lost (<see cref="ScheduleTab.Unplayed"/>) it is a match with no result, when the season has both teams.
    /// </summary>
    private void AddSchedules()
    {
        foreach (var (sourceId, tab, matches) in _schedules)
        {
            var workbook = tab.Season ?? sourceId;
            var regular = _seasons.FirstOrDefault(s => !s.IsPlayoffs && string.Equals(s.SourceWorkbook, workbook, StringComparison.OrdinalIgnoreCase)
                && (tab.Tab is null || string.Equals(s.SourceSheet, tab.Tab, StringComparison.OrdinalIgnoreCase)));
            if (matches is null || regular is null)
            {
                _warnings.Add($"{sourceId}: schedule '{tab.Sheet}' not read ({(matches is null ? "no such sheet" : $"no season from {workbook}")})");
                continue;
            }
            var key = HistoricalKeys.SeasonSourceId(regular.SourceWorkbook!, regular.SourceSheet!);
            var names = SeasonTeamNames.Of(SeasonOf(regular), tab.Teams);
            // The season's undated games, a series' as one: what the meetings date, in their lists' order
            var undated = _games
                .Where(g => string.Equals(g.SeasonSourceId, key, StringComparison.OrdinalIgnoreCase) && g.Teams.Count == 2)
                .GroupBy(g => g.Series ?? g.SourceKey, StringComparer.Ordinal)
                .Where(g => g.All(game => game.PlayedAt is null))
                .Select(g => (Games: g.ToList(), IsSeries: g.First().Series is not null, Pair: Pair(g.First().Teams[0].Name, g.First().Teams[1].Name)))
                .ToList();
            var meetings = matches
                .Select(m => (m.PlayedAt, Home: ScheduledTeam(names, m.First), Away: ScheduledTeam(names, m.Second)))
                .GroupBy(m => (m.PlayedAt.Date, Pair: Pair(m.Home, m.Away)))
                .Select(g => (PlayedAt: g.Min(m => m.PlayedAt), g.Key.Pair, g.First().Home, g.First().Away, Count: g.Count()))
                .OrderBy(m => m.PlayedAt)
                .ToList();
            var left = 0;
            foreach (var meeting in meetings)
            {
                var games = 0;
                while (games < meeting.Count && undated.FindIndex(u => u.Pair == meeting.Pair) is var index and >= 0)
                {
                    foreach (var game in undated[index].Games)
                        game.PlayedAt = meeting.PlayedAt;
                    games += undated[index].Games.Count;
                    undated.RemoveAt(index);
                }
                if (games > 0)
                    continue;
                if (tab.Unplayed && names.Knows(meeting.Home) && names.Knows(meeting.Away))
                    _scheduled.Add(new ArchiveScheduledMatchInput
                    {
                        SeasonSourceId = key,
                        SourceKey = $"{sourceId}/{tab.Sheet}@{meeting.PlayedAt:yyyy-MM-dd}",
                        Home = meeting.Home,
                        Away = meeting.Away,
                        PlayedAt = meeting.PlayedAt,
                    });
                else
                    left++;
            }
            if (left > 0)
                _warnings.Add($"{sourceId}/{tab.Sheet}: {left} of {meetings.Count} scheduled meetings date no game");
            if (_changedSourceIds?.Contains(sourceId) != false)
                _affected.Add(key);
        }
    }

    /// <summary>Two teams in either order, as one key.</summary>
    private static (string, string) Pair(string? a, string? b)
    {
        var (x, y) = ((a ?? "").Trim().ToUpperInvariant(), (b ?? "").Trim().ToUpperInvariant());
        return string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
    }

    /// <summary>The season's team a schedule names: "ASP (Dhmon)" is ASP's, or (no team of that name) Dhmon's, the captain's.</summary>
    private static string ScheduledTeam(SeasonTeamNames names, string scheduled)
    {
        var bracketed = Bracketed().Match(scheduled);
        if (!bracketed.Success)
            return names.Resolve(scheduled);
        var bare = names.Resolve(Bracketed().Replace(scheduled, "").Trim());
        if (names.Knows(bare))
            return bare;
        var captain = names.Resolve(bracketed.Groups[1].Value.Trim());
        return names.Knows(captain) ? captain : names.Resolve(scheduled);
    }

    [GeneratedRegex(@"\s*\(([^)]*)\)")]
    private static partial Regex Bracketed();
}
