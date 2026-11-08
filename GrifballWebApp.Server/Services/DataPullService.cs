using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using System.Collections.Concurrent;

namespace GrifballWebApp.Server.Services;

public class DataPullService : IDataPullService
{
    private readonly ILogger<DataPullService> _logger;
    private readonly IHaloInfiniteClient _haloInfiniteClient;
    private readonly GrifballContext _grifballContext;
    private readonly IGetsertXboxUserService _getsertXboxUserService;

    private readonly HaloCallPacer _pacer;
    // Decodes each match saved; reads a game variant's layout once for every match of it the pull saves
    private readonly CustomScoreLayouts _scoreLayouts;

    public DataPullService(ILogger<DataPullService> logger, IHaloInfiniteClient haloInfiniteClient, GrifballContext grifballContext, IGetsertXboxUserService getsertXboxUserService,
        HaloCallPacer? pacer = null)
    {
        _logger = logger;
        _haloInfiniteClient = haloInfiniteClient;
        _grifballContext = grifballContext;
        _getsertXboxUserService = getsertXboxUserService;
        _pacer = pacer ?? new HaloCallPacer(0);
        _scoreLayouts = new CustomScoreLayouts(grifballContext);
    }

    /// <summary>
    /// Pulls the players' recent custom games (pages <paramref name="startPage"/> to <paramref name="endPage"/> of their
    /// match histories, <paramref name="perPage"/> each), fetches the Grifball ones not saved yet, and saves them. Its Halo
    /// calls are paced to the gateway's quota (<see cref="HaloCallPacer"/>). A player's pages are read two at a time: an
    /// empty page ends the history, so the pages after it are skipped, never the ones before it (they're kept). Returns
    /// what it found and saved, and what it couldn't fetch.
    /// </summary>
    public Task<MatchPullResult> DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default)
        => PullAsync(xboxIDs, startPage, endPage, perPage, null, ct);

    /// <summary>Games a page of a player's history holds (Halo's most).</summary>
    public const int HistoryPageSize = 25;

    /// <summary>The most Halo calls a pull between dates makes for one player: finding the pages, then reading them.</summary>
    public const int DefaultMaxCallsBack = 100;

    /// <summary>
    /// Pulls the players' custom games played between <paramref name="from"/> and <paramref name="to"/> (an old season), as
    /// <see cref="DownloadRecentMatchesForPlayers"/> does their recent ones, without reading each history from its newest page:
    /// a history is newest first, so the first page reaching back to <paramref name="to"/> is found by reading pages 1, 2, 4,
    /// 8… until one does, then halving the gap between the last two; from it, pages are read until one reaches back past
    /// <paramref name="from"/>. A few calls find a season under thousands of newer games. A player whose search takes more
    /// than <paramref name="maxCalls"/> calls is counted in <see cref="MatchPullResult.CappedHistories"/>.
    /// </summary>
    public Task<MatchPullResult> DownloadMatchesBetween(List<long> xboxIDs, DateTime from, DateTime to, int maxCalls = DefaultMaxCallsBack, CancellationToken ct = default)
        => PullAsync(xboxIDs, 0, 0, HistoryPageSize, (from, to, maxCalls), ct);

    private async Task<MatchPullResult> PullAsync(List<long> xboxIDs, int startPage, int endPage, int perPage,
        (DateTime From, DateTime To, int MaxCalls)? between, CancellationToken ct)
    {
        if (xboxIDs.Any() is false)
            return MatchPullResult.Empty;
        ct.ThrowIfCancellationRequested();
        var stringXboxIDs = xboxIDs.Distinct().Select(x => $"xuid({x})").ToList();

        var matchIDBag = new ConcurrentBag<Guid>();
        var failedHistories = new ConcurrentBag<string>();
        var cappedHistories = new ConcurrentBag<string>();

        var parallelOptions = new ParallelOptions()
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = 2,
        };

        await Parallel.ForEachAsync(stringXboxIDs, parallelOptions, async (xboxUserID, outerCt) =>
        {
            if (between is { } window)
            {
                var read = await ReadBetweenAsync(xboxUserID, window.From, window.To, window.MaxCalls, outerCt);
                foreach (var guid in read.Games)
                    matchIDBag.Add(guid);
                if (read.Failed)
                    failedHistories.Add(xboxUserID);
                if (read.Capped)
                    cappedHistories.Add(xboxUserID);
                if (read.NoGames)
                    _logger.LogInformation("No recent custom games for {XboxUserID}", xboxUserID);
                return;
            }

            // The last page worth reading: the first found empty (the history ends there). The pages after it are skipped,
            // never the ones before it.
            var lastPage = int.MaxValue;
            var noGames = false;
            var innerParallelOptions = new ParallelOptions()
            {
                CancellationToken = outerCt,
                MaxDegreeOfParallelism = 2,
            };

            await Parallel.ForAsync(startPage, endPage + 1, innerParallelOptions, async (page, innerCt) =>
            {
                // Decided once its turn comes: an earlier page may have ended the history meanwhile
                await _pacer.WaitAsync(innerCt);
                if (page > Volatile.Read(ref lastPage))
                    return;

                var records = await HistoryPageAsync(xboxUserID, page, Math.Max(page * perPage - 1, 0), perPage, innerCt);
                if (records is null)
                {
                    failedHistories.Add(xboxUserID);
                    return;
                }

                if (records.Count == 0)
                {
                    // Lowered to this page unless an earlier one is already there
                    int seen;
                    while (page < (seen = Volatile.Read(ref lastPage)) && Interlocked.CompareExchange(ref lastPage, page, seen) != seen)
                    {
                    }
                    if (page == startPage)
                        noGames = true;
                    return;
                }

                foreach (var guid in GrifballGames(records, xboxUserID))
                    matchIDBag.Add(guid);
            });

            if (noGames)
                _logger.LogInformation("No recent custom games for {XboxUserID}", xboxUserID);
        });

        var matchIDs = matchIDBag.Distinct().ToList();
        var found = matchIDs.Count;

        var matchesAlreadyDownloaded = await _grifballContext.Matches
            .Where(x => matchIDs.Contains(x.MatchID))
            .Select(x => x.MatchID)
            .ToArrayAsync(ct);

        matchIDs = matchIDs.Except(matchesAlreadyDownloaded).ToList();

        var matchStatsBag = new ConcurrentBag<MatchStats>();
        var failedMatches = new ConcurrentBag<Guid>();

        await Parallel.ForEachAsync(matchIDs, parallelOptions, async (matchID, matchCt) =>
        {
            await _pacer.WaitAsync(matchCt);
            MatchStats? matchStats;
            try
            {
                matchStats = await GetMatch(matchID);
            }
            catch (Exception e) when (!matchCt.IsCancellationRequested)
            {
                // No answer: this game is missing, not the whole pull
                _logger.LogWarning(e, "Could not get match {MatchID}: no answer", matchID);
                matchStats = null;
            }

            if (matchStats is null)
                failedMatches.Add(matchID);
            else
                matchStatsBag.Add(matchStats);
        });

        var xboxUserIds = matchStatsBag
            .SelectMany(x => x.Players)
            .Where(p => p.BotAttributes is null)
            .Select(p => long.Parse(p.PlayerId.RemoveXUIDWrapper()))
            .Distinct()
            .ToList();

        var existingIds = await _grifballContext.XboxUsers
            .Where(x => xboxUserIds.Contains(x.XboxUserID))
            .Select(x => x.XboxUserID)
            .ToListAsync(ct);

        var needToPull = xboxUserIds.Except(existingIds).ToList();

        // Grab all missing xbox users now in one go to avoid harsh rate limits when pulling one at a time
        await _getsertXboxUserService.GetsertXboxUsersByXuid([.. needToPull], ct);
        // Subs and opponents nobody signed up as get a placeholder, so their games count in stats; their sign-up merges
        // it. Not in SaveMatchStats: the historical import pulls through it and ties its own placeholders to the XUIDs.
        await new GrifballWebApp.Server.Historical.GamertagMatching(_grifballContext).EnsureXboxPlaceholdersAsync(xboxUserIds, ct);

        // Read once for every match, rather than once a player a match in SaveMatchStats. A query, not the players
        // themselves: one the lookups above couldn't save is looked up (and saved) on its own as before.
        var knownXboxUserIDs = (await _grifballContext.XboxUsers
            .Where(x => xboxUserIds.Contains(x.XboxUserID))
            .Select(x => x.XboxUserID)
            .ToListAsync(ct)).ToHashSet();

        // Could also do this in parallel foreach but would need a seperate context for each.
        // Each save looked through every entity the context tracks for changes, the games saved before it too, so each was
        // slower than the last. A game's entities are added, not changed: nothing to look for. Back on after, for the caller.
        var saved = 0;
        var detectChanges = _grifballContext.ChangeTracker.AutoDetectChangesEnabled;
        _grifballContext.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var matchStats in matchStatsBag)
            {
                if (await SaveMatch(matchStats, knownXboxUserIDs))
                    saved++;
            }
        }
        finally
        {
            _grifballContext.ChangeTracker.AutoDetectChangesEnabled = detectChanges;
        }

        return new MatchPullResult(stringXboxIDs.Count, [.. failedHistories.Distinct()], found, matchesAlreadyDownloaded.Length, saved,
            [.. failedMatches], matchStatsBag.Count - saved, [.. cappedHistories.Distinct()]);
    }

    /// <summary>What reading a player's history between two dates found.</summary>
    private sealed record HistoryRead(List<Guid> Games, bool Failed, bool Capped, bool NoGames);

    /// <summary>
    /// The Grifball games a player played between <paramref name="from"/> and <paramref name="to"/>, found as
    /// <see cref="DownloadMatchesBetween"/> says: pages 1, 2, 4, 8… until one reaches back to <paramref name="to"/>, the gap
    /// between the last two halved down to the first such page, then pages read from it until one reaches back past
    /// <paramref name="from"/>. A page is asked for once, whatever looks at it.
    /// </summary>
    private async Task<HistoryRead> ReadBetweenAsync(string player, DateTime from, DateTime to, int maxCalls, CancellationToken ct)
    {
        var pages = new Dictionary<int, IReadOnlyList<PlayerMatchHistoryRecord>>();
        var games = new List<Guid>();
        var calls = 0;
        var failed = false;
        var capped = false;

        // A page of the history (newest first); null when Halo didn't give it, or the calls ran out
        async Task<IReadOnlyList<PlayerMatchHistoryRecord>?> Page(int page)
        {
            if (pages.TryGetValue(page, out var known))
                return known;
            if (calls >= maxCalls)
            {
                capped = true;
                return null;
            }
            calls++;
            await _pacer.WaitAsync(ct);
            var records = await HistoryPageAsync(player, page, page * HistoryPageSize, HistoryPageSize, ct);
            if (records is null)
                failed = true;
            else
                pages[page] = records;
            return records;
        }

        HistoryRead Read(bool noGames = false) => new(games, failed, capped, noGames);
        DateTime Oldest(IReadOnlyList<PlayerMatchHistoryRecord> page) => page.Min(x => x.MatchInfo.StartTime).UtcDateTime;
        // Reaches back to the window's end: its oldest game is at or before it, or the history has ended
        bool Reaches(IReadOnlyList<PlayerMatchHistoryRecord> page) => page.Count == 0 || Oldest(page) <= to;

        var newest = await Page(0);
        if (newest is null)
            return Read();
        if (newest.Count == 0)
            return Read(noGames: true);

        var first = 0;
        if (!Reaches(newest))
        {
            // Pages 1, 2, 4, 8… until one reaches it: the first that does is after `before` and at most `after`
            int before = 0, after = 1;
            while (true)
            {
                var page = await Page(after);
                if (page is null)
                    return Read();
                if (Reaches(page))
                    break;
                before = after;
                after *= 2;
            }
            while (after - before > 1)
            {
                var middle = before + (after - before) / 2;
                var page = await Page(middle);
                if (page is null)
                    return Read();
                if (Reaches(page))
                    after = middle;
                else
                    before = middle;
            }
            first = after;
        }

        // The window's pages: from the first reaching its end, until one reaches back past its start (or the history ends)
        for (var number = first; ; number++)
        {
            var page = await Page(number);
            if (page is null)
                return Read();
            if (page.Count == 0)
                break;
            games.AddRange(GrifballGames(page, player));
            if (Oldest(page) < from)
                break;
        }
        return Read();
    }

    /// <summary>A page of the player's custom game history, newest first; null (logged) when Halo didn't give it.</summary>
    private async Task<IReadOnlyList<PlayerMatchHistoryRecord>?> HistoryPageAsync(string player, int page, int start, int count, CancellationToken ct)
    {
        HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer> response;
        try
        {
            response = await _haloInfiniteClient.StatsGetMatchHistory(player, start, count, Surprenant.Grunt.Models.HaloInfinite.MatchType.Custom);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // No answer (the gateway unreachable, a timeout): this page is missing, not the whole pull
            _logger.LogWarning(e, "Could not get page {Page} of the match history of {XboxUserID}: no answer", page, player);
            return null;
        }
        ct.ThrowIfCancellationRequested();

        if (response.Result is null)
        {
            _logger.LogWarning("Could not get page {Page} of the match history of {XboxUserID}: {Error}", page, player,
                HaloErrors.Describe(response.Error?.Message));
            return null;
        }
        return response.Result.Results;
    }

    /// <summary>The Grifball games among a page of the player's history (variant category 41).</summary>
    private IEnumerable<Guid> GrifballGames(IEnumerable<PlayerMatchHistoryRecord> records, string player) =>
        records.Where(x => (int?)x.MatchInfo.GameVariantCategory is 41).Select(x =>
        {
            var parsed = Guid.TryParse(x.MatchId, out var guid);
            if (parsed is false)
                _logger.LogWarning("Could not parse Guid {Guid} from player history for player {XboxUserID}", x.MatchId, player);
            return guid;
        }).Where(x => x != default).ToList();

    public async Task<MatchStats?> GetMatch(Guid matchID)
    {
        var response = await _haloInfiniteClient.StatsGetMatchStats(matchID);

        if (response.Result is null)
        {
            _logger.LogWarning("Could not get match {MatchID}: {Error}", matchID, HaloErrors.Describe(response.Error?.Message));
            return null;
        }

        return response.Result;
    }

    public async Task GetAndSaveMatch(Guid matchID)
    {
        //await _grifballContext.Matches.Where(x => x.MatchID == matchID).ExecuteDeleteAsync();
        if (await _grifballContext.Matches.AnyAsync(m => m.MatchID == matchID))
        {
            _logger.LogDebug("Match {MatchID} already exists", matchID);
            return;
        }
        
        var matchStats = await GetMatch(matchID);

        if (matchStats is null)
            return;

        await SaveMatchStats(matchStats);
    }

    /// <param name="knownXboxUserIDs">Players whose Xbox user is saved already: tied by ID, not looked up one by one.</param>
    public Task SaveMatchStats(MatchStats matchStats, IReadOnlySet<long>? knownXboxUserIDs = null) => SaveMatch(matchStats, knownXboxUserIDs);

    /// <summary>Saves the game; false when it isn't saved (not a team game).</summary>
    private async Task<bool> SaveMatch(MatchStats matchStats, IReadOnlySet<long>? knownXboxUserIDs)
    {
        var matchID = matchStats.MatchId;

        var teams = matchStats.Teams.Select(x =>
        {
            var outcome = x.Outcome switch
            {
                1 => Outcomes.Tie,
                2 => Outcomes.Won,
                3 => Outcomes.Lost,
                4 => Outcomes.DidNotFinish,
                _ => throw new ArgumentOutOfRangeException(nameof(x.Outcome), x.Outcome, "Outcome out of range"),
            };
            return new MatchTeamResult(x.TeamId, x.Stats.CoreStats.Score, outcome);
        }).ToList();

        if (teams.Any() is false)
        {
            _logger.LogDebug("Ignoring non team match {MatchID}", matchID);
            return false;
        }

        var match = Match.FromHalo(matchID, matchStats.MatchInfo.StartTime.UtcDateTime, matchStats.MatchInfo.EndTime.UtcDateTime,
            matchStats.MatchInfo.Duration, DateTime.UtcNow, matchStats.MatchInfo.UgcGameVariant?.AssetId, matchStats.MatchInfo.UgcGameVariant?.VersionId, teams);

        // One player at a time: each may be looked up (and saved) on its own
        foreach (var x in matchStats.Players.Where(p => p.BotAttributes is null))
        {
            var xuid = x.PlayerId.RemoveXUIDWrapper();

            if (!long.TryParse(xuid, out var xuidLong))
            {
                _logger.LogError("Could not parse {XUID}, not long", xuid);
                throw new Exception("XUID not long");
            }
            var xboxUser = knownXboxUserIDs?.Contains(xuidLong) is true ? null : await _getsertXboxUserService.GetsertXboxUserByXuid(xuidLong);

            var stats = x.PlayerTeamStats.Where(pts => pts.TeamId == x.LastTeamId).FirstOrDefault()?.Stats.CoreStats
                ?? throw new Exception("Failed to find last team stats");

            if (x.PlayerTeamStats.Any(pts => pts.TeamId != x.LastTeamId))
            {
                _logger.LogWarning("Player {PlayerID} played on multiple teams in match {MatchID}", x.PlayerId, matchID);
            }

            if (match.MatchTeams.All(t => t.TeamID != x.LastTeamId))
                throw new Exception("Missing team!!!");

            match.AddParticipant(x.LastTeamId, xuidLong, new MatchParticipantStats
            {
                Score = stats.Score,
                PersonalScore = stats.PersonalScore,
                Kills = stats.Kills,
                Deaths = stats.Deaths,
                Assists = stats.Assists,
                Kda = stats.KDA,
                Suicides = stats.Suicides,
                Betrayals = stats.Betrayals,
                AverageLife = stats.AverageLifeDuration,
                MeleeKills = stats.MeleeKills,
                PowerWeaponKills = stats.PowerWeaponKills,
                ShotsFired = stats.ShotsFired,
                ShotsHit = stats.ShotsHit,
                Accuracy = stats.Accuracy,
                DamageDealt = stats.DamageDealt,
                DamageTaken = stats.DamageTaken,
                CalloutAssists = stats.CalloutAssists,
                MaxKillingSpree = stats.MaxKillingSpree,
                FirstJoinedTime = x.ParticipationInfo.FirstJoinedTime.UtcDateTime,
                JoinedInProgress = x.ParticipationInfo.JoinedInProgress,
                LastLeaveTime = x.ParticipationInfo.LastLeaveTime.HasValue ? x.ParticipationInfo.LastLeaveTime.Value.UtcDateTime : null,
                LeftInProgress = x.ParticipationInfo.LeftInProgress,
                PresentAtBeginning = x.ParticipationInfo.PresentAtBeginning,
                PresentAtCompletion = x.ParticipationInfo.PresentAtCompletion,
                TimePlayed = x.ParticipationInfo.TimePlayed,
                RoundsLost = stats.RoundsLost,
                RoundsTied = stats.RoundsTied,
                RoundsWon = stats.RoundsWon,
                Rank = x.Rank,
                Spawns = stats.Spawns,
                ObjectivesCompleted = stats.ObjectivesCompleted,
            }, stats.Medals.Select(m => new MedalCount(m.NameId, m.Count, m.TotalPersonalScoreAwarded)), xboxUser);
        }
        // Its goals and the mode's other stats, from Score and PersonalScore (#217), set while the game is built and before
        // any of it is tracked. Decoding only reads saved matches, so it adds nothing a failed save would have to let go.
        await _scoreLayouts.ApplyAsync(match);
        // Nothing of the game is added until every player is resolved: a lookup that fails (the gateway down) then leaves
        // nothing tracked for a retry to trip over, and a lookup's own save can't store the game without its players.
        // The match brings its teams, their players and their medals.
        _grifballContext.Matches.Add(match);
        try
        {
            await _grifballContext.SaveChangesAsync();
        }
        catch
        {
            // A save that fails leaves the game added. Only what this game added is let go, so a retry can add it
            // again and whatever else the caller has tracked stays as it was.
            ForgetAdded(match);
            throw;
        }
        return true;
    }

    private void ForgetAdded(Match match)
    {
        var added = new List<object> { match };
        added.AddRange(match.MatchTeams);
        added.AddRange(match.Participants);
        added.AddRange(match.Participants.SelectMany(p => p.MedalEarned));
        added.AddRange(match.Participants.Select(p => p.XboxUser).Where(u => u is not null));
        foreach (var entry in added.Select(o => _grifballContext.Entry(o)).Where(e => e.State is EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }

    public async Task DownloadMedals()
    {
        if (await _grifballContext.Medals.AnyAsync())
        {
            throw new Exception("You already have medals!");
        }
        var response = await _haloInfiniteClient.Medals();

        if (response.Result is null)
        {
            throw new Exception("Result null. Failed to get medals");
        }

        var medalTypes = response.Result.Types.Select((medalTypeName, index) => MedalType.Imported(index + 1, medalTypeName)).ToList();

        var medalDifficulties = response.Result.Difficulties.Select((medalTypeDifficulty, index) => MedalDifficulty.Imported(index + 1, medalTypeDifficulty)).ToList();

        await _grifballContext.MedalTypes.AddRangeAsync(medalTypes);

        await _grifballContext.MedalDifficulties.AddRangeAsync(medalDifficulties);

        var medals = response.Result.Medals.Select(medal => Database.Models.Medal.Imported(medal.NameID, medal.Name.Value, medal.Description.Value, medal.SpriteIndex,
            medal.SortingWeight, medalDifficultyID: medal.DifficultyIndex + 1, medalTypeID: medal.TypeIndex + 1, medal.PersonalScore)).ToList();

        await _grifballContext.Medals.AddRangeAsync(medals);

        await _grifballContext.SaveChangesAsync();
    }

    /// <summary>How many games a backfill saves at a time: what it fetched is kept when it's stopped.</summary>
    internal const int BackfillChunk = 25;

    /// <summary>
    /// Fetches the stats of up to <paramref name="batchSize"/> saved matches that have no game variant yet, through the
    /// same paced Halo calls as the pulls (<see cref="HaloCallPacer"/>), and sets their variant: only
    /// <see cref="Match.UgcGameVariantAssetID"/> and <see cref="Match.UgcGameVariantVersionID"/>, never the stats saved
    /// before. Idempotent and resumable: a match that has its variant is never asked for again, and one Halo didn't give
    /// stays without it for the next run. Matches are taken in a random order, so the same few failing ones can't
    /// hold up the rest. Reports (done, of) after each chunk of games saved.
    /// </summary>
    public async Task<VariantBackfillResult> BackfillGameVariants(int batchSize, Action<int, int>? progress = null, CancellationToken ct = default)
    {
        var missing = await _grifballContext.Matches
            .Where(Match.WithoutGameVariant)
            .Select(m => m.MatchID)
            .ToListAsync(ct);
        if (missing.Count == 0)
            return new VariantBackfillResult(0, 0, 0, 0, []);

        var batch = missing.OrderBy(_ => Random.Shared.Next()).Take(Math.Max(batchSize, 0)).ToList();
        var parallelOptions = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 2 };
        var filled = 0;
        var noVariant = 0;
        var failed = new List<Guid>();
        var versions = new HashSet<Guid>();

        foreach (var chunk in batch.Chunk(BackfillChunk))
        {
            var fetched = new ConcurrentDictionary<Guid, MatchStats?>();
            await Parallel.ForEachAsync(chunk, parallelOptions, async (matchID, matchCt) =>
            {
                await _pacer.WaitAsync(matchCt);
                try
                {
                    fetched[matchID] = await GetMatch(matchID);
                }
                catch (Exception e) when (!matchCt.IsCancellationRequested)
                {
                    _logger.LogWarning(e, "Could not get match {MatchID} for its game variant: no answer", matchID);
                    fetched[matchID] = null;
                }
            });

            var matches = await _grifballContext.Matches
                .Where(Match.WithoutGameVariant).Where(m => chunk.Contains(m.MatchID))
                .ToListAsync(ct);
            foreach (var match in matches)
            {
                var variant = fetched.GetValueOrDefault(match.MatchID)?.MatchInfo?.UgcGameVariant;
                if (fetched.GetValueOrDefault(match.MatchID) is null)
                    failed.Add(match.MatchID);
                else if (variant is null)
                    noVariant++;
                else
                {
                    match.GiveGameVariant(variant.AssetId, variant.VersionId);
                    versions.Add(variant.VersionId);
                    filled++;
                }
            }
            await _grifballContext.SaveChangesAsync(ct);

            var done = filled + noVariant + failed.Count;
            _logger.LogInformation("Game variant backfill: {Done} of {Batch} matches looked up ({Filled} given their variant, {Failed} Halo didn't give); {Left} matches had none before this run",
                done, batch.Count, filled, failed.Count, missing.Count);
            progress?.Invoke(done, batch.Count);
        }

        return new VariantBackfillResult(missing.Count, batch.Count, filled, noVariant, failed, versions);
    }
}

/// <summary>What a run of the game variant backfill did (<see cref="IDataPullService.BackfillGameVariants"/>).</summary>
/// <param name="Missing">Matches without a game variant when it started.</param>
/// <param name="Attempted">Of those, the ones it looked up this run.</param>
/// <param name="Filled">The ones it gave their variant.</param>
/// <param name="NoVariant">The ones Halo gave without a variant: left as they were.</param>
/// <param name="Failed">The ones Halo didn't give: tried again on a later run.</param>
/// <param name="Versions">The variant versions it filled in.</param>
public sealed record VariantBackfillResult(int Missing, int Attempted, int Filled, int NoVariant, IReadOnlyList<Guid> Failed,
    IReadOnlyCollection<Guid>? Versions = null)
{
    /// <summary>Matches still without a variant after the run.</summary>
    public int Left => Missing - Filled;

    public string Summary
    {
        get
        {
            var text = $"{Filled} of {Attempted} matches given their game variant, {Left} left";
            if (Failed.Count > 0)
                text += $"; {Failed.Count} Halo didn't give (a later run tries them again)";
            if (NoVariant > 0)
                text += $"; {NoVariant} without a variant";
            return text;
        }
    }
}

public interface IDataPullService
{
    Task<MatchPullResult> DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default);
    Task<MatchPullResult> DownloadMatchesBetween(List<long> xboxIDs, DateTime from, DateTime to, int maxCalls = DataPullService.DefaultMaxCallsBack, CancellationToken ct = default);
    Task<MatchStats?> GetMatch(Guid matchID);
    Task GetAndSaveMatch(Guid matchID);
    Task SaveMatchStats(MatchStats matchStats, IReadOnlySet<long>? knownXboxUserIDs = null);
    Task DownloadMedals();
    Task<VariantBackfillResult> BackfillGameVariants(int batchSize, Action<int, int>? progress = null, CancellationToken ct = default);
}
/// <summary>What a pull of players' recent games did (<see cref="IDataPullService.DownloadRecentMatchesForPlayers"/>).</summary>
/// <param name="Players">The players looked through.</param>
/// <param name="FailedHistories">The players a page of whose match history Halo didn't give: their games there are missing.</param>
/// <param name="MatchesFound">The Grifball games in their histories.</param>
/// <param name="MatchesAlreadySaved">Of those, the ones saved before.</param>
/// <param name="MatchesFetched">The new ones fetched and saved.</param>
/// <param name="FailedMatches">The new ones Halo didn't give (after the gateway client's retries, or no answer): not saved.</param>
/// <param name="MatchesNotTeamGames">The new ones fetched but not saved: not team games (a Grifball variant played free for all).</param>
/// <param name="CappedHistories">Pulled between dates: the players whose search took more than its most calls, so their games then may be missing.</param>
public sealed record MatchPullResult(int Players, IReadOnlyList<string> FailedHistories, int MatchesFound, int MatchesAlreadySaved, int MatchesFetched,
    IReadOnlyList<Guid> FailedMatches, int MatchesNotTeamGames = 0, IReadOnlyList<string>? CappedHistories = null)
{
    public static readonly MatchPullResult Empty = new(0, [], 0, 0, 0, []);

    /// <summary>Everything found was fetched: no history page or game missing.</summary>
    public bool Complete => FailedHistories.Count == 0 && FailedMatches.Count == 0;

    public string Summary
    {
        get
        {
            var text = $"{Players} players, {MatchesFound} Grifball games found, {MatchesAlreadySaved} saved before, {MatchesFetched} new saved";
            if (FailedMatches.Count > 0)
                text += $"; {FailedMatches.Count} games Halo didn't give, not saved (the next pull tries them again)";
            if (FailedHistories.Count > 0)
                text += $"; {FailedHistories.Count} players' match histories incomplete";
            if (MatchesNotTeamGames > 0)
                text += $"; {MatchesNotTeamGames} not team games, not saved";
            if (CappedHistories is { Count: > 0 } capped)
                text += $"; {capped.Count} players' histories too long to search back to the season";
            return text;
        }
    }
}
