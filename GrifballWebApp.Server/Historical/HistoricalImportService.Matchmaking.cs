using GrifballWebApp.Database.Models;
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
        if (season is null)
        {
            _context.MatchmakingSeasons.Add(season = new MatchmakingSeason { HistoricalSourceID = group.SourceId });
            counts.MatchmakingSeasonsCreated++;
        }
        else
            counts.MatchmakingSeasonsUpdated++;

        var (start, end, dated) = await SeasonDates(group, ct);
        season.StartsAt = start;
        // Undated: a re-run keeps the end an earlier one gave, as a season's does
        season.EndsAt = !dated && season.EndsAt > start && season.EndsAt <= end ? season.EndsAt : end;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (season.EndsAt > now)
            season.EndsAt = now;
        season.Name = group.SeasonName;
        season.GameVersionID = group.GameVersion?.GameVesionID;
        season.HistoricalSourceUrl = group.SourceUrl;
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
                var row = new MatchmakingPlayerStat
                {
                    MatchmakingSeasonID = season.MatchmakingSeasonID,
                    UserID = userId,
                    GameVersionID = group.EntryGames[entry].GameVesionID,
                };
                SetTotals(row, player);
                if (existing.TryGetValue(userId, out var stored))
                {
                    row.MatchmakingPlayerStatID = stored.MatchmakingPlayerStatID;
                    (row.CreatedAt, row.CreatedByID, row.ModifiedAt, row.ModifiedByID) = (stored.CreatedAt, stored.CreatedByID, stored.ModifiedAt, stored.ModifiedByID);
                    // Only the values that changed are written: a re-run of the same archive changes nothing
                    _context.Entry(stored).CurrentValues.SetValues(row);
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
            .Where(g => g.Origin != RecordedGameOrigin.Manual && (keys.Contains(g.SourceKey) || g.MatchedMatch!.MatchmakingSeasonID == season.MatchmakingSeasonID))
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

            if (!bySourceKey.TryGetValue(key, out var game))
                _context.RecordedGames.Add(game = new RecordedGame { SourceKey = key });
            kept.Add(game);
            // A game an earlier import put in a season (Late League was one) is in none now
            if (game.SeriesGame is not null)
                await Unlink(game, ct);
            game.SeasonID = null;
            ApplyRecordedGame(game, input, gameVersion, sides, sides.ToDictionary(s => s.Side, _ => (int?)null), players);
            game.IsPlayoffs = false;

            var match = game.MatchedMatch;
            var startedAt = input.PlayedAt ?? season.StartsAt;
            if (match is null)
                _context.MatchedMatches.Add(match = MatchedMatch.Archive(game, season.MatchmakingSeasonID, startedAt));
            else
                match.RecordArchived(game, season.MatchmakingSeasonID, startedAt);
            SyncMatchedPlayers(match.HomeTeam, players.Where(p => p.Input.Side == sides[0].Side).Select(p => p.UserId));
            SyncMatchedPlayers(match.AwayTeam, players.Where(p => p.Input.Side == sides[1].Side).Select(p => p.UserId));
            counts.RecordedGamesImported++;
        }

        foreach (var stale in existing.Where(g => !kept.Contains(g)).ToList())
        {
            // A game of this season is a matched match, in no series (it left any when it came in)
            if (stale.MatchedMatch is { } match)
                RemoveMatchedMatch(match);
            _context.RecordedGames.Remove(stale);
            counts.RecordedGamesRemoved++;
        }
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>A side's matched players become these users: added, and removed for those no longer on it.</summary>
    private void SyncMatchedPlayers(MatchedTeam side, IEnumerable<int> userIds)
    {
        var users = userIds.ToHashSet();
        foreach (var stale in side.Players.Where(p => !users.Contains(p.UserID)).ToList())
        {
            side.Players.Remove(stale);
            _context.MatchedPlayers.Remove(stale);
        }
        foreach (var userId in users.Where(u => !side.Players.Any(p => p.UserID == u)))
            side.Players.Add(new MatchedPlayer { UserID = userId });
    }

    /// <summary>A matched match goes with its two sides and their players (an archive game has no votes).</summary>
    private void RemoveMatchedMatch(MatchedMatch match)
    {
        _context.MatchedPlayers.RemoveRange(match.HomeTeam.Players.Concat(match.AwayTeam.Players));
        _context.MatchedMatches.Remove(match);
        _context.MatchedTeams.RemoveRange(match.HomeTeam, match.AwayTeam);
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

    /// <summary>A recorded game's facts, sides (each with its season team, if any) and players, updated in place: sides by
    /// number and players by user, so a re-run of the same archive changes nothing.</summary>
    private static void ApplyRecordedGame(RecordedGame game, RecordedGameInput input, GameVersion gameVersion, List<RecordedTeamInput> sides, IReadOnlyDictionary<int, int?> sideTeams, List<(RecordedPlayerInput Input, int UserId)> players)
    {
        game.GameVersionID = gameVersion.GameVesionID;
        game.Map = Truncate(input.Map, 100);
        game.Gametype = Truncate(input.Gametype, 100);
        game.PlayedAt = input.PlayedAt;
        game.IsPlayoffs = input.IsPlayoffs;

        foreach (var stale in game.Teams.Where(t => !sides.Any(s => s.Side == t.Side)).ToList())
            game.Teams.Remove(stale);
        foreach (var side in sides)
        {
            var row = game.Teams.FirstOrDefault(t => t.Side == side.Side);
            if (row is null)
                game.Teams.Add(row = new RecordedGameTeam { Side = side.Side });
            row.TeamID = sideTeams[side.Side];
            row.Name = side.Name is null ? null : Truncate(HistoricalKeys.CleanGamertag(side.Name), Team.TeamNameColumnLength);
            row.Outcome = side.Outcome;
            row.Score = side.Score;
        }
        foreach (var stale in game.Participants.Where(p => !players.Any(x => x.UserId == p.UserID)).ToList())
            game.Participants.Remove(stale);
        foreach (var (player, userId) in players)
        {
            var row = game.Participants.FirstOrDefault(p => p.UserID == userId);
            if (row is null)
                game.Participants.Add(row = new RecordedGameParticipant { UserID = userId });
            row.RecordedGameTeam = game.Teams.FirstOrDefault(t => t.Side == player.Side);
            row.Gamertag = Truncate(HistoricalKeys.CleanGamertag(player.Gamertag), 50)!;
            row.Kills = player.Kills;
            row.Deaths = player.Deaths;
            row.Assists = player.Assists;
            row.Goals = player.Goals;
            row.Score = player.Score;
            row.CarrierKills = player.CarrierKills;
            row.Betrayals = player.Betrayals;
            row.Suicides = player.Suicides;
            row.HammerKills = player.HammerKills;
            row.SwordKills = player.SwordKills;
            row.BallKills = player.BallKills;
            row.Punches = player.Punches;
        }
    }

    /// <summary>A sheet row's totals, as every stored-totals table keeps them.</summary>
    private static void SetTotals(PlayerStatTotals row, ArchivePlayer player)
    {
        row.Gamertag = HistoricalKeys.CleanGamertag(player.Gamertag);
        row.GamesPlayed = Int(player.GamesPlayed);
        row.Wins = Int(player.Wins);
        row.Losses = Int(player.Losses);
        row.WinPercent = Dec(player.WinPercent, 3);
        row.Goals = Int(player.Goals);
        row.Kills = Int(player.Kills);
        row.Deaths = Int(player.Deaths);
        row.Assists = Int(player.Assists);
        row.KdRatio = Dec(player.KdRatio, 3);
        row.KdSpread = IntOrNull(player.KdSpread);
        row.CarrierKills = Int(player.CarrierKills);
        row.AverageLifeSeconds = Dec(player.AverageLifeSeconds, 2);
        row.TotalSecondsPlayed = IntOrNull(player.TotalSecondsPlayed);
        row.Sprees = Int(player.Sprees);
        row.MultiKills = Int(player.MultiKills);
        row.Exterminations = Int(player.Exterminations);
        row.Betrayals = Int(player.Betrayals);
        row.HammerKills = IntOrNull(player.HammerKills);
        row.SwordKills = IntOrNull(player.SwordKills);
        row.BallKills = IntOrNull(player.BallKills);
        row.Punches = IntOrNull(player.Punches);
        row.Score = IntOrNull(player.Score);
        row.DamageDealt = IntOrNull(player.DamageDealt);
        row.DamageTaken = IntOrNull(player.DamageTaken);
        row.ExtendedMetricsJson = player.ExtendedMetrics is { ValueKind: JsonValueKind.Object } metrics && metrics.EnumerateObject().Any()
            ? metrics.GetRawText()
            : null;
    }
}
