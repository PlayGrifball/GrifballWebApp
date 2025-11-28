using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models.HaloInfinite;
using System.Collections.Concurrent;

namespace GrifballWebApp.Server.Services;

public class DataPullService : IDataPullService
{
    private readonly ILogger<DataPullService> _logger;
    private readonly IHaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly GrifballContext _grifballContext;
    private readonly IGetsertXboxUserService _getsertXboxUserService;

    public DataPullService(ILogger<DataPullService> logger, IHaloInfiniteClientFactory haloInfiniteClientFactory, GrifballContext grifballContext, IGetsertXboxUserService getsertXboxUserService)
    {
        _logger = logger;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _grifballContext = grifballContext;
        _getsertXboxUserService = getsertXboxUserService;
    }
    public async Task DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default)
    {
        if (xboxIDs.Any() is false)
            return;
        ct.ThrowIfCancellationRequested();
        var stringXboxIDs = xboxIDs.Distinct().Select(x => $"xuid({x})").ToList();

        var matchIDBag = new ConcurrentBag<Guid>();

        var parallelOptions = new ParallelOptions()
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = 2,
        };

        await Parallel.ForEachAsync(stringXboxIDs, parallelOptions, async (xboxUserID, outerCt) =>
        {
            using var source = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(source.Token, outerCt);
            var linkedToken = linked.Token;
            var innerParallelOptions = new ParallelOptions()
            {
                CancellationToken = linkedToken,
                MaxDegreeOfParallelism = 2,
            };
            try
            {
                await Parallel.ForAsync(startPage, endPage + 1, innerParallelOptions, async (page, innerCt) =>
                {
                    if (source.IsCancellationRequested is true)
                    {
                        _logger.LogWarning("Caught error while looking for matches {XboxUserID}. Will continue looking for other users matches", xboxUserID);
                        return;
                    }

                    var start = Math.Max(page * perPage - 1, 0);
                    var response = await _haloInfiniteClientFactory.StatsGetMatchHistory(xboxUserID, start, perPage, Surprenant.Grunt.Models.HaloInfinite.MatchType.Custom);
                    innerCt.ThrowIfCancellationRequested();

                    if (response.Result is null)
                    {
                        _logger.LogWarning("Failed to get match history for user {XboxUserID}", xboxUserID);
                        return;
                    }

                    if (response.Result.Results.Any() is false)
                    {
                        _logger.LogWarning("Detected 0 matches for user {XboxUserID} bailing out", xboxUserID);
                        await source.CancelAsync(); // Stop request matches for this user
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
            }
            catch when (source.IsCancellationRequested is true)
            {
                _logger.LogWarning("Caught error while looking for matches {XboxUserID}. Will continue looking for other users matches", xboxUserID);
            }
        });

        var matchIDs = new List<Guid>();
        matchIDs = matchIDBag.Distinct().ToList();

        var matchesAlreadyDownloaded = await _grifballContext.Matches
            .Where(x => matchIDs.Contains(x.MatchID))
            .Select(x => x.MatchID)
            .ToArrayAsync(ct);

        matchIDs = matchIDs.Except(matchesAlreadyDownloaded).ToList();

        var matchStatsBag = new ConcurrentBag<MatchStats>();

        await Parallel.ForEachAsync(matchIDs, parallelOptions, async (matchID, ct) =>
        {
            var matchStats = await GetMatch(matchID);

            if (matchStats is null)
            {
                _logger.LogWarning("Failed to get match {MatchID}", matchID);
            }
            else
            {
                matchStatsBag.Add(matchStats);
            }
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

        // Could also do this in parallel foreach but would need a seperate context for each
        foreach (var matchStats in matchStatsBag)
        {
            // Let's also pass down the client to avoid creating new one (although it shouldn't be needed since we just fetched all the xbox users)
            await SaveMatchStats(matchStats);
        }
    }

    public async Task<MatchStats?> GetMatch(Guid matchID)
    {
        var response = await _haloInfiniteClientFactory.StatsGetMatchStats(matchID);

        if (response.Result is null)
        {
            _logger.LogWarning("Match {MatchID} not found", matchID);
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

    public async Task SaveMatchStats(MatchStats matchStats)
    {
        var matchID = matchStats.MatchId;

        var match = new Match()
        {
            MatchID = matchID,
            StartTime = matchStats.MatchInfo.StartTime.ToUniversalTime().DateTime,
            EndTime = matchStats.MatchInfo.EndTime.ToUniversalTime().DateTime,
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
            return;
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
            var xboxUser = await _getsertXboxUserService.GetsertXboxUserByXuid(xuidLong);

            var stats = x.PlayerTeamStats.Where(pts => pts.TeamId == x.LastTeamId).FirstOrDefault()?.Stats.CoreStats
                ?? throw new Exception("Failed to find last team stats");

            if (x.PlayerTeamStats.Any(pts => pts.TeamId != x.LastTeamId))
            {
                _logger.LogWarning("Player {PlayerID} played on multiple teams in match {MatchID}", x.PlayerId, matchID);
            }

            return new MatchParticipant()
            {
                XboxUser = xboxUser,
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
                FirstJoinedTime = x.ParticipationInfo.FirstJoinedTime.ToUniversalTime().DateTime,
                JoinedInProgress = x.ParticipationInfo.JoinedInProgress,
                LastLeaveTime = x.ParticipationInfo.LastLeaveTime.HasValue ? x.ParticipationInfo.LastLeaveTime.Value.ToUniversalTime().DateTime : null,
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
                    XboxUserID = xboxUser.XboxUserID,
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
    }

    public async Task DownloadMedals()
    {
        if (await _grifballContext.Medals.AnyAsync())
        {
            throw new Exception("You already have medals!");
        }
        var response = await _haloInfiniteClientFactory.Medals();

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
    Task DownloadRecentMatchesForPlayers(List<long> xboxIDs, int startPage = 0, int endPage = 10, int perPage = 25, CancellationToken ct = default);
    Task<MatchStats?> GetMatch(Guid matchID);
    Task GetAndSaveMatch(Guid matchID);
    Task SaveMatchStats(MatchStats matchStats);
    Task DownloadMedals();
}