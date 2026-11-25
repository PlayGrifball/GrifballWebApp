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
    /// tab's games each their own; <see cref="ScheduleTab.WholeSeries"/>: an entry each match, all its games). A meeting
    /// with nothing left to date is left out, and said in a warning; of a season whose
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
            _scheduledSeasons.Add(key);
            var names = SeasonTeamNames.Of(SeasonOf(regular), tab.Teams);
            var undated = Undated(key, SeasonOf(regular), names);
            var meetings = Meetings(matches, names);
            var left = 0;
            foreach (var meeting in meetings.Where(m => !DateGames(m, undated, tab.WholeSeries)))
            {
                if (tab.Unplayed && names.Knows(meeting.Home) && names.Knows(meeting.Away))
                    _scheduled.Add(new ArchiveScheduledMatchInput
                    {
                        SeasonSourceId = key,
                        SourceKey = $"{sourceId}/{tab.Sheet}@{meeting.Day:yyyy-MM-dd}",
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

    /// <summary>A schedule's meeting: its two teams (the season's names, home first) on a calendar day, at its first match's
    /// time, and how many entries it has.</summary>
    private sealed record Meeting(DateTime Day, DateTime PlayedAt, (string, string) Teams, string Home, string Away, int Count);

    /// <summary>A season's undated games, a series' as one unit, in their lists' order: what the meetings date.</summary>
    private List<(List<RecordedGameInput> Games, (string, string) Pair)> Undated(string key, List<ArchiveSeason> season, SeasonTeamNames names)
    {
        // A per-game tab's side is its players' team (its rows name no team)
        var teamOf = season.SelectMany(s => s.Players).Where(p => !HistoricalKeys.IsExcludedTeamName(p.TeamName))
            .GroupBy(p => HistoricalKeys.Normalize(p.Gamertag)).ToDictionary(g => g.Key, g => g.First().TeamName!.Trim());
        return [.. _games
            .Where(g => string.Equals(g.SeasonSourceId, key, StringComparison.OrdinalIgnoreCase) && g.Teams.Count == 2)
            .GroupBy(g => g.Series ?? g.SourceKey, StringComparer.Ordinal)
            .Where(g => g.All(game => game.PlayedAt is null))
            .Select(g => (Games: g.ToList(), Pair: Pair(SideTeam(g.First(), 0, teamOf, names), SideTeam(g.First(), 1, teamOf, names))))];
    }

    /// <summary>The schedule's matches as meetings: the same two teams' on one calendar day are one, in time order.</summary>
    private static List<Meeting> Meetings(List<CalendarMatch> matches, SeasonTeamNames names)
        => [.. matches
            .Select(m => (m.Day, m.PlayedAt, Home: ScheduledTeam(names, m.First), Away: ScheduledTeam(names, m.Second)))
            .GroupBy(m => (m.Day, Pair: Pair(m.Home, m.Away)))
            .Select(g => new Meeting(g.Key.Day, g.Min(m => m.PlayedAt), g.Key.Pair, g.First().Home, g.First().Away, g.Count()))
            .OrderBy(m => m.PlayedAt)];

    /// <summary>A meeting dates the first undated units of its teams until it has dated as many games as it has entries (a
    /// whole series' run of units an entry); false when it dated none. What it dates leaves <paramref name="undated"/>.</summary>
    private static bool DateGames(Meeting meeting, List<(List<RecordedGameInput> Games, (string, string) Pair)> undated, bool wholeSeries)
    {
        var (entries, dated) = (0, false);
        while (entries < meeting.Count && undated.FindIndex(u => u.Pair == meeting.Teams) is var index and >= 0)
        {
            // A whole match: its teams' undated games (or one-game series) in a row
            var end = index + 1;
            while (wholeSeries && end < undated.Count && undated[end].Pair == meeting.Teams)
                end++;
            foreach (var game in undated.Skip(index).Take(end - index).SelectMany(u => u.Games))
                game.PlayedAt = meeting.PlayedAt;
            entries += wholeSeries ? 1 : undated[index].Games.Count;
            undated.RemoveRange(index, end - index);
            dated = true;
        }
        return dated;
    }

    /// <summary>A game's side's team: its name as the season names the team (a per-game block's title's "ND"), or (a per-game
    /// tab's side, unnamed) the team most of its players are on; "" when its players are on no team.</summary>
    private static string SideTeam(RecordedGameInput game, int index, Dictionary<string, string> teamOf, SeasonTeamNames names)
    {
        var side = game.Teams[index];
        if (side.Name is { } name)
            return names.Resolve(name);
        return game.Players.Where(p => p.Side == side.Side)
            .Select(p => teamOf.GetValueOrDefault(HistoricalKeys.Normalize(p.Gamertag)))
            .OfType<string>().GroupBy(t => t, StringComparer.OrdinalIgnoreCase).OrderByDescending(t => t.Count()).Select(t => t.Key).FirstOrDefault() ?? "";
    }

    /// <summary>Two teams in either order, as one key.</summary>
    private static (string, string) Pair(string a, string b)
    {
        var (x, y) = (a.Trim().ToUpperInvariant(), b.Trim().ToUpperInvariant());
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
