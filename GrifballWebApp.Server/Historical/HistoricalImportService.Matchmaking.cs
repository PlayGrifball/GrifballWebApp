using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GrifballWebApp.Server.Historical;

public partial class HistoricalImportService
{
    /// <summary>
    /// A Late League season: matchmaking, not a season of the app's, so a <see cref="MatchmakingSeason"/> found again by its
    /// key. Its players' sheet totals are its <see cref="MatchmakingPlayerStat"/> rows (by player), and each of its recorded
    /// games is a finished <see cref="MatchedMatch"/> of its two sides, as the queue's matches are: no teams, standings,
    /// playoffs or champion. What this run doesn't bring back (a row, a game) goes. One transaction, under the dry run and the
    /// guardrail like a season's (its stat rows are what removals are measured by).
    /// </summary>
    private async Task ImportMatchmakingSeason(SeasonGroup group, List<GameVersion> gameVersions, HistoricalImportReport report, HistoricalImportRunOptions options, CancellationToken ct)
    {
        var counts = new HistoricalImportReport();
        var removal = new HistoricalSeasonRemoval { SeasonKey = group.SourceId, SeasonName = group.SeasonName };
        removal.StatsBefore = await _context.MatchmakingPlayerStats.CountAsync(s => s.MatchmakingSeason.HistoricalSourceID == group.SourceId, ct);
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var season = await UpsertMatchmakingSeason(group, counts, ct);
            var resolver = new PlayerResolver(_context, 0, season.StartsAt, season.MatchmakingSeasonID);
            await ImportMatchmakingStats(group, season, resolver, counts, ct);
            if (group.RecordedGames is not null)
                await ImportMatchmakingGames(group, season, gameVersions, resolver, counts, ct);
            foreach (var gamertag in resolver.Ambiguous.Distinct(StringComparer.OrdinalIgnoreCase))
                Warn(counts, $"{group.SourceId}: {gamertag} is in more than one Xbox account's gamertag history for the season; left to its placeholder");

            await _context.SaveChangesAsync(ct);
            counts.DummyUsersCreated = resolver.DummiesCreated;
            removal.StatsRemoved = counts.StatsRemoved;
            removal.Stopped = !options.DryRun && options.Guardrail?.Exceeded(removal) == true;
            if (options.DryRun || removal.Stopped)
            {
                await transaction.RollbackAsync(ct);
                _context.ChangeTracker.Clear();
                report.SeasonRemovals.Add(removal);
                if (removal.Stopped)
                {
                    Warn(report, $"{group.SourceId}: stopped for review, it would remove {removal.Summary}");
                    return;
                }
                Add(report, counts);
                return;
            }
            await transaction.CommitAsync(ct);
            _context.ChangeTracker.Clear();
            if (removal.RemovesAnything)
                report.SeasonRemovals.Add(removal);
            Add(report, counts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Historical import of the matchmaking season {Season} failed and was rolled back", group.SourceId);
            report.Errors.Add($"{group.SourceId}: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>The matchmaking season, found by its key or made, with its name, game, source and the days its games were
    /// played (as a season's: <see cref="SeasonDates"/>), ending by now at the latest.</summary>
    private async Task<MatchmakingSeason> UpsertMatchmakingSeason(SeasonGroup group, HistoricalImportReport counts, CancellationToken ct)
    {
        var season = await _context.MatchmakingSeasons.FirstOrDefaultAsync(s => s.HistoricalSourceID == group.SourceId, ct);
        var gameVersionID = group.GameVersion?.GameVesionID;
        if (season is null)
        {
            _context.MatchmakingSeasons.Add(season = MatchmakingSeason.Imported(group.SourceId, group.SeasonName, gameVersionID, group.SourceUrl));
            counts.MatchmakingSeasonsCreated++;
        }
        else
        {
            season.UpdateArchived(group.SeasonName, gameVersionID, group.SourceUrl);
            counts.MatchmakingSeasonsUpdated++;
        }

        // Undated, a re-run keeps the end an earlier one gave, and it began and ended by now, as a season's does
        var (start, end, dated) = await SeasonDates(group, ct);
        season.DateArchived(start, end, dated, _timeProvider.GetUtcNow().UtcDateTime);
        await _context.SaveChangesAsync(ct);
        return season;
    }

    /// <summary>The sheet's rows, one per player (a second row for the same player is reported and skipped); the season's
    /// rows this run didn't write go. A player's games, where the season has any, are their stats instead (SeasonStatsService
    /// reads them first), so the row is kept as the sheet's record.</summary>
    private async Task ImportMatchmakingStats(SeasonGroup group, MatchmakingSeason season, PlayerResolver resolver, HistoricalImportReport counts, CancellationToken ct)
    {
        var existing = await _context.MatchmakingPlayerStats.Where(s => s.MatchmakingSeasonID == season.MatchmakingSeasonID)
            .ToDictionaryAsync(s => s.UserID, ct);
        var kept = new HashSet<int>();
        foreach (var entry in group.Entries)
        {
            foreach (var player in entry.Players.Where(p => !string.IsNullOrWhiteSpace(p.Gamertag)))
            {
                var userId = (await resolver.Resolve(player.Gamertag, null, ct)).UserId;
                if (!kept.Add(userId))
                {
                    Skip(counts, $"{group.SourceId}: {player.Gamertag} is a second row for the same player");
                    continue;
                }
                var row = MatchmakingPlayerStat.FromSheet(season.MatchmakingSeasonID, userId, group.EntryGames[entry].GameVesionID, Totals(player));
                if (existing.TryGetValue(userId, out var stored))
                {
                    // Only the values that changed are written: a re-run of the same archive changes nothing
                    stored.Restate(row);
                    counts.StatsUpdated++;
                }
                else
                {
                    _context.MatchmakingPlayerStats.Add(row);
                    counts.StatsInserted++;
                }
            }
        }
        var stale = existing.Values.Where(s => !kept.Contains(s.UserID)).ToList();
        _context.MatchmakingPlayerStats.RemoveRange(stale);
        counts.StatsRemoved += stale.Count;
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The season's recorded games, upserted by their SourceKey (in no season: <see cref="RecordedGame.SeasonID"/> null),
    /// each a finished matched match of the season: home its side 1, away its side 2, each side's players its matched
    /// players. The season's games the archive no longer lists go, with their matches.
    /// </summary>
    private async Task ImportMatchmakingGames(SeasonGroup group, MatchmakingSeason season, List<GameVersion> gameVersions, PlayerResolver resolver, HistoricalImportReport counts, CancellationToken ct)
    {
        var inputs = group.RecordedGames!;
        var keys = inputs.Select(i => i.SourceKey.Trim()).ToList();
        var existing = await _context.RecordedGames
            // Never a game entered by hand: not the archive's, whatever its key
            .Where(RecordedGame.Imported)
            .Where(g => keys.Contains(g.SourceKey) || g.MatchedMatch!.MatchmakingSeasonID == season.MatchmakingSeasonID)
            .Include(g => g.Teams).Include(g => g.Participants).Include(g => g.SeriesGame)
            .Include(g => g.MatchedMatch!).ThenInclude(m => m.HomeTeam).ThenInclude(t => t.Players)
            .Include(g => g.MatchedMatch!).ThenInclude(m => m.AwayTeam).ThenInclude(t => t.Players)
            .ToListAsync(ct);
        var bySourceKey = existing.ToDictionary(g => g.SourceKey, StringComparer.OrdinalIgnoreCase);
        var kept = new HashSet<RecordedGame>(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in inputs)
        {
            var key = input.SourceKey.Trim();
            var gameVersion = gameVersions.FirstOrDefault(g => string.Equals(g.Slug, input.GameVersion, StringComparison.OrdinalIgnoreCase));
            if (gameVersion is null)
            {
                Skip(counts, $"Recorded game {key}: unknown game version '{input.GameVersion}'");
                continue;
            }
            if (!seen.Add(key))
            {
                Skip(counts, $"Recorded game {key}: listed twice");
                continue;
            }
            var players = await RecordedPlayers(input, key, resolver, counts, ct);
            var sides = RecordedSides(input);
            if (sides.Count != 2)
            {
                Skip(counts, $"Recorded game {key}: a matchmaking game has two sides, it has {sides.Count}");
                continue;
            }
            // No series to put a Waypoint game in, and the queue's matches come from the bot: kept as the archive records it
            if (input.WaypointMatchId is not null)
                Warn(counts, $"Recorded game {key}: has Waypoint match {input.WaypointMatchId}; a Late League game is kept as the archive records it");

            // No playoffs in matchmaking, and in no season
            var facts = RecordedFacts(input, gameVersion) with { IsPlayoffs = false };
            if (!bySourceKey.TryGetValue(key, out var game))
                _context.RecordedGames.Add(game = RecordedGame.Import(key, facts, null));
            kept.Add(game);
            // A game an earlier import put in a season (Late League was one) is in none now
            if (game.SeriesGame is not null)
                await Unlink(game, ct);
            game.Reimport(facts, null);
            ApplyRecordedGame(game, sides, sides.ToDictionary(s => s.Side, _ => (int?)null), players);

            var match = game.MatchedMatch;
            var startedAt = input.PlayedAt ?? season.StartsAt;
            if (match is null)
                _context.MatchedMatches.Add(match = MatchedMatch.Archive(game, season.MatchmakingSeasonID, startedAt));
            else
                match.RecordArchived(game, season.MatchmakingSeasonID, startedAt);
            match.SetArchivedRoster(
                players.Where(p => p.Input.Side == sides[0].Side).Select(p => p.UserId),
                players.Where(p => p.Input.Side == sides[1].Side).Select(p => p.UserId));
            counts.RecordedGamesImported++;
        }

        foreach (var stale in existing.Where(g => !kept.Contains(g)).ToList())
        {
            // A game of this season is a matched match, in no series (it left any when it came in)
            if (stale.MatchedMatch is { } match)
                _context.RemoveArchivedMatch(match);
            _context.RemoveRecordedGame(stale);
            counts.RecordedGamesRemoved++;
        }
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>A recorded game's players, each resolved as the sheets' are (a placeholder when nobody matches) and taken
    /// once: a second line for the same player is reported and skipped.</summary>
    private async Task<List<(RecordedPlayerInput Input, int UserId)>> RecordedPlayers(RecordedGameInput input, string key, PlayerResolver resolver, HistoricalImportReport counts, CancellationToken ct)
    {
        var players = new List<(RecordedPlayerInput Input, int UserId)>();
        foreach (var player in input.Players.Where(p => !string.IsNullOrWhiteSpace(p.Gamertag)))
        {
            var userId = (await resolver.Resolve(player.Gamertag, null, ct)).UserId;
            if (players.Any(p => p.UserId == userId))
            {
                Skip(counts, $"Recorded game {key}: {player.Gamertag} is listed twice for one player");
                continue;
            }
            players.Add((player, userId));
        }
        return players;
    }

    /// <summary>A recorded game's sides by number; a side listed twice is taken once.</summary>
    private static List<RecordedTeamInput> RecordedSides(RecordedGameInput input)
        => input.Teams.GroupBy(t => t.Side).Select(g => g.First()).OrderBy(t => t.Side).ToList();

    /// <summary>A recorded game's facts as the archive gives them: its map and gametype cut to what a row holds.</summary>
    private static RecordedGameFacts RecordedFacts(RecordedGameInput input, GameVersion gameVersion) => new(
        gameVersion.GameVesionID,
        Truncate(input.Map, RecordedGame.MapMaxLength),
        Truncate(input.Gametype, RecordedGame.GametypeMaxLength),
        input.PlayedAt,
        input.IsPlayoffs);

    /// <summary>A recorded game's sides (each with its season team, if any) and players, updated in place by the game:
    /// sides by number and players by user, so a re-run of the same archive changes nothing. A side whose result the
    /// archive doesn't give has none (<see cref="Outcomes.DidNotFinish"/>).</summary>
    private static void ApplyRecordedGame(RecordedGame game, List<RecordedTeamInput> sides, IReadOnlyDictionary<int, int?> sideTeams, List<(RecordedPlayerInput Input, int UserId)> players)
    {
        game.SetSides(sides.Select(side => new RecordedSideFacts(
            side.Side,
            sideTeams[side.Side],
            side.Name is null ? null : Truncate(HistoricalKeys.CleanGamertag(side.Name), Team.TeamNameColumnLength),
            Enum.IsDefined(side.Outcome) ? side.Outcome : Outcomes.DidNotFinish,
            side.Score)).ToList());
        game.SetPlayers(players.Select(p => new RecordedPlayerLine(p.UserId, p.Input.Side, Truncate(HistoricalKeys.CleanGamertag(p.Input.Gamertag), 50)!,
            p.Input.Kills, p.Input.Deaths, p.Input.Assists)
        {
            Goals = p.Input.Goals,
            Score = p.Input.Score,
            CarrierKills = p.Input.CarrierKills,
            Betrayals = p.Input.Betrayals,
            Suicides = p.Input.Suicides,
            HammerKills = p.Input.HammerKills,
            SwordKills = p.Input.SwordKills,
            BallKills = p.Input.BallKills,
            Punches = p.Input.Punches,
        }).ToList());
    }

    /// <summary>A sheet row's totals, as every stored-totals table keeps them.</summary>
    private static StatSheetTotals Totals(ArchivePlayer player) => new()
    {
        Gamertag = HistoricalKeys.CleanGamertag(player.Gamertag),
        GamesPlayed = Int(player.GamesPlayed),
        Wins = Int(player.Wins),
        Losses = Int(player.Losses),
        WinPercent = Dec(player.WinPercent, 3),
        Goals = Int(player.Goals),
        Kills = Int(player.Kills),
        Deaths = Int(player.Deaths),
        Assists = Int(player.Assists),
        KdRatio = Dec(player.KdRatio, 3),
        KdSpread = IntOrNull(player.KdSpread),
        CarrierKills = Int(player.CarrierKills),
        AverageLifeSeconds = Dec(player.AverageLifeSeconds, 2),
        TotalSecondsPlayed = IntOrNull(player.TotalSecondsPlayed),
        Sprees = Int(player.Sprees),
        MultiKills = Int(player.MultiKills),
        Exterminations = Int(player.Exterminations),
        Betrayals = Int(player.Betrayals),
        HammerKills = IntOrNull(player.HammerKills),
        SwordKills = IntOrNull(player.SwordKills),
        BallKills = IntOrNull(player.BallKills),
        Punches = IntOrNull(player.Punches),
        Score = IntOrNull(player.Score),
        DamageDealt = IntOrNull(player.DamageDealt),
        DamageTaken = IntOrNull(player.DamageTaken),
        ExtendedMetricsJson = player.ExtendedMetrics is { ValueKind: JsonValueKind.Object } metrics && metrics.EnumerateObject().Any()
            ? metrics.GetRawText()
            : null,
    };
}
