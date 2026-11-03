using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;
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

    public DataPullService(ILogger<DataPullService> logger, IHaloInfiniteClient haloInfiniteClient, GrifballContext grifballContext, IGetsertXboxUserService getsertXboxUserService,
        HaloCallPacer? pacer = null)
    {
        _logger = logger;
        _haloInfiniteClient = haloInfiniteClient;
        _grifballContext = grifballContext;
        _getsertXboxUserService = getsertXboxUserService;
        _pacer = pacer ?? new HaloCallPacer(0);
    }

    /// <summary>
    /// Pulls the players' recent custom games (pages <paramref name="startPage"/> to <paramref name="endPage"/> of their
    /// match histories, <paramref name="perPage"/> each), fetches the Grifball ones not saved yet, and saves them. Its Halo
    /// calls are paced to the gateway's quota (<see cref="HaloCallPacer"/>). A player's pages are read two at a time: an
    /// empty page ends the history, so the pages after it are skipped, never the ones before it (they're kept). Returns
    /// what it found and saved, and what it couldn't fetch.
    /// </summary>
    public async Task<MatchPullResult> DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default)
    {
        if (xboxIDs.Any() is false)
            return MatchPullResult.Empty;
        ct.ThrowIfCancellationRequested();
        var stringXboxIDs = xboxIDs.Distinct().Select(x => $"xuid({x})").ToList();

        var matchIDBag = new ConcurrentBag<Guid>();
        var failedHistories = new ConcurrentBag<string>();

        var parallelOptions = new ParallelOptions()
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = 2,
        };

        await Parallel.ForEachAsync(stringXboxIDs, parallelOptions, async (xboxUserID, outerCt) =>
        {
            // The first page found empty: the history ends there
            var firstEmptyPage = int.MaxValue;
            var innerParallelOptions = new ParallelOptions()
            {
                CancellationToken = outerCt,
                MaxDegreeOfParallelism = 2,
            };

            await Parallel.ForAsync(startPage, endPage + 1, innerParallelOptions, async (page, innerCt) =>
            {
                if (page > Volatile.Read(ref firstEmptyPage))
                    return;
                await _pacer.WaitAsync(innerCt);
                if (page > Volatile.Read(ref firstEmptyPage))
                    return;

                var start = Math.Max(page * perPage - 1, 0);
                HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer> response;
                try
                {
                    response = await _haloInfiniteClient.StatsGetMatchHistory(xboxUserID, start, perPage, Surprenant.Grunt.Models.HaloInfinite.MatchType.Custom);
                }
                catch (Exception e) when (!innerCt.IsCancellationRequested)
                {
                    // No answer (the gateway unreachable, a timeout): this page is missing, not the whole pull
                    _logger.LogWarning(e, "Could not get page {Page} of the match history of {XboxUserID}: no answer", page, xboxUserID);
                    failedHistories.Add(xboxUserID);
                    return;
                }
                innerCt.ThrowIfCancellationRequested();

                if (response.Result is null)
                {
                    _logger.LogWarning("Could not get page {Page} of the match history of {XboxUserID}: {Error}", page, xboxUserID,
                        HaloErrors.Describe(response.Error?.Message));
                    failedHistories.Add(xboxUserID);
                    return;
                }

                if (response.Result.Results.Any() is false)
                {
                    // Lowered to this page unless an earlier one is already there
                    int seen;
                    while (page < (seen = Volatile.Read(ref firstEmptyPage)) && Interlocked.CompareExchange(ref firstEmptyPage, page, seen) != seen)
                    {
                    }
                    return;
                }

                var guids = response.Result.Results.Where(x => (int?)x.MatchInfo.GameVariantCategory is 41).Select(x =>
                {
                    var stringMatchID = x.MatchId;
                    var parsed = Guid.TryParse(stringMatchID, out var guid);
                    if (parsed is false)
                        _logger.LogWarning("Could not parse Guid {Guid} from player history for player {XboxUserID}", stringMatchID, xboxUserID);
                    return guid;
                }).Where(x => x != default).ToArray();

                foreach (var guid in guids)
                    matchIDBag.Add(guid);
            });

            if (firstEmptyPage == startPage)
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

        // Every player has an Xbox user now (fetched above, or a placeholder): SaveMatchStats ties them by ID rather than
        // looking each one up again
        var knownXboxUserIDs = xboxUserIds.ToHashSet();

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
            [.. failedMatches], matchStatsBag.Count - saved);
    }

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

        var match = new Match()
        {
            MatchID = matchID,
            StartTime = matchStats.MatchInfo.StartTime.UtcDateTime,
            EndTime = matchStats.MatchInfo.EndTime.UtcDateTime,
            Duration = matchStats.MatchInfo.Duration,
            StatsPullDate = DateTime.UtcNow,
        };

        var matchTeams = matchStats.Teams.Select(x =>
        {
            var outcome = x.Outcome switch
            {
                1 => Outcomes.Tie,
                2 => Outcomes.Won,
                3 => Outcomes.Lost,
                4 => Outcomes.DidNotFinish,
                _ => throw new ArgumentOutOfRangeException(nameof(x.Outcome), x.Outcome, "Outcome out of range"),
            };
            return new MatchTeam()
            {
                Match = match,
                TeamID = x.TeamId,
                Score = x.Stats.CoreStats.Score,
                Outcome = outcome,
            };
        }).ToList();

        if (matchTeams.Any() is false)
        {
            _logger.LogDebug("Ignoring non team match {MatchID}", matchID);
            return false;
        }

        await _grifballContext.MatchTeams.AddRangeAsync(matchTeams);

        match.MatchTeams = matchTeams;

        var matchParticpants = await matchStats.Players.Where(p => p.BotAttributes is null).Select(async x =>
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

            return new MatchParticipant()
            {
                XboxUserID = xuidLong,
                XboxUser = xboxUser!,
                //MatchID = matchID,
                //TeamID = x.LastTeamId,
                MatchTeam = matchTeams.FirstOrDefault(t => t.TeamID == x.LastTeamId) ?? throw new Exception("Missing team!!!"),
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
                MedalEarned = stats.Medals.Select(m => new MedalEarned()
                {
                    MedalID = m.NameId,
                    MatchID = matchID,
                    XboxUserID = xuidLong,
                    Count = m.Count,
                    TotalPersonalScoreAwarded = m.TotalPersonalScoreAwarded,
                }).ToList(),

            };
        }).ToAsyncEnumerable().SelectAwait(async x => await x).ToListAsync();
        _grifballContext.MatchParticipants.AddRange(matchParticpants);

        var statusBefore = _grifballContext.Entry(match).State;
        // GesertXboxUser may call save changes, do not want to add match more than once
        if (statusBefore is EntityState.Detached)
        {
            _grifballContext.Matches.Add(match);
        }
        await _grifballContext.SaveChangesAsync();
        return true;
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

        var medalTypes = response.Result.Types.Select((medalTypeName, index) => new MedalType()
        {
            MedalTypeID = index + 1,
            MedalTypeName = medalTypeName,
        }).ToList();

        var medalDifficulties = response.Result.Difficulties.Select((medalTypeDifficulty, index) => new MedalDifficulty()
        {
            MedalDifficultyID = index + 1,
            MedalDifficultyName = medalTypeDifficulty,
        }).ToList();

        await _grifballContext.MedalTypes.AddRangeAsync(medalTypes);

        await _grifballContext.MedalDifficulties.AddRangeAsync(medalDifficulties);

        var medals = response.Result.Medals.Select(medal => new Database.Models.Medal()
        {
            MedalID = medal.NameID,
            MedalName = medal.Name.Value,
            Description = medal.Description.Value,
            SpriteIndex = medal.SpriteIndex,
            SortingWeight = medal.SortingWeight,
            MedalDifficultyID = medal.DifficultyIndex + 1,
            MedalTypeID = medal.TypeIndex + 1,
            PersonalScore = medal.PersonalScore,
        }).ToList();

        await _grifballContext.Medals.AddRangeAsync(medals);

        await _grifballContext.SaveChangesAsync();
    }
}

public interface IDataPullService
{
    Task<MatchPullResult> DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default);
    Task<MatchStats?> GetMatch(Guid matchID);
    Task GetAndSaveMatch(Guid matchID);
    Task SaveMatchStats(MatchStats matchStats, IReadOnlySet<long>? knownXboxUserIDs = null);
    Task DownloadMedals();
}
/// <summary>What a pull of players' recent games did (<see cref="IDataPullService.DownloadRecentMatchesForPlayers"/>).</summary>
/// <param name="Players">The players looked through.</param>
/// <param name="FailedHistories">The players a page of whose match history Halo didn't give: their games there are missing.</param>
/// <param name="MatchesFound">The Grifball games in their histories.</param>
/// <param name="MatchesAlreadySaved">Of those, the ones saved before.</param>
/// <param name="MatchesFetched">The new ones fetched and saved.</param>
/// <param name="FailedMatches">The new ones Halo didn't give (after the gateway client's retries, or no answer): not saved.</param>
/// <param name="MatchesNotTeamGames">The new ones fetched but not saved: not team games (a Grifball variant played free for all).</param>
public sealed record MatchPullResult(int Players, IReadOnlyList<string> FailedHistories, int MatchesFound, int MatchesAlreadySaved, int MatchesFetched,
    IReadOnlyList<Guid> FailedMatches, int MatchesNotTeamGames = 0)
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
            return text;
        }
    }
}
