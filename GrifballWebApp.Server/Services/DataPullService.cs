using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Services;

public class DataPullService
{
    private readonly ILogger<DataPullService> _logger;
    private readonly HaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly GrifballContext _grifballContext;

    public DataPullService(ILogger<DataPullService> logger, HaloInfiniteClientFactory haloInfiniteClientFactory, GrifballContext grifballContext)
    {
        _logger = logger;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _grifballContext = grifballContext;
    }
    public async Task DownloadMatch(Guid matchID)
    {
        if (await _grifballContext.Matches.AnyAsync(m => m.MatchID == matchID))
        {
            _logger.LogDebug("Match {MatchID} already exists", matchID);
            return;
        }
        var client = await _haloInfiniteClientFactory.CreateAsync();

        var response = await client.StatsGetMatchStats(matchID);

        if (response.Result is null)
        {
            _logger.LogWarning("Match {MatchID} not found", matchID);
            return;
        }

        var playerIDs = response.Result.Players.Select(x => x.PlayerId.RemoveXUIDWrapper()).ToList();
        var users = await client.Users(playerIDs);

        var match = new Match()
        {
            MatchID = matchID,
            StartTime = response.Result.MatchInfo.StartTime.ToUniversalTime().DateTime,
            EndTime = response.Result.MatchInfo.EndTime.ToUniversalTime().DateTime,
        };

        var matchParticpants = response.Result.Players.Select(x =>
        {
            var xuid = x.PlayerId.RemoveXUIDWrapper();

            if (!long.TryParse(xuid, out var xuidLong))
            {
                _logger.LogError("Could not parse {XUID}, not long", xuid);
                throw new Exception("XUID not long");
            }
            var xboxUser = _grifballContext.XboxUsers.FirstOrDefault(x => x.XUID == xuidLong);

            if (xboxUser is null) // Needs to be created
            {
                var user = users.Result.FirstOrDefault(u => u.xuid == xuid);
                if (user is null)
                {
                    _logger.LogError("User {UserID} not found", x.PlayerId);
                    throw new Exception("Failed to find user");
                }

                xboxUser = new XboxUser()
                {
                    XUID = xuidLong,
                    Gamertag = user.gamertag,
                };
            }

            var stats = x.PlayerTeamStats.Where(pts => pts.TeamId == x.LastTeamId).FirstOrDefault()?.Stats.CoreStats
                ?? throw new Exception("Failed to find last team stats");

            if (x.PlayerTeamStats.Any(pts => pts.TeamId != x.LastTeamId))
            {
                _logger.LogWarning("Player {PlayerID} played on multiple teams in match {}", x.PlayerId, matchID);
            }

            return new MatchParticipant()
            {
                XboxUser = xboxUser,
                TeamID = x.LastTeamId,
                Kills = stats.Kills,
                Deaths = stats.Deaths,
                Assists = stats.Assists,
                Kda = stats.KDA,
                Suicides = stats.Suicides,
                Betrayals = stats.Betrayals,
                AverageLife = stats.AverageLifeDuration,
                GrenadeKills = stats.GrenadeKills,
                HeadshotKills = stats.HeadshotKills,
                MeleeKills = stats.MeleeKills,
                PowerWeaponKills = stats.PowerWeaponKills,
                ShotsFired = stats.ShotsFired,
                ShotsHit = stats.ShotsHit,
                Accuracy = stats.Accuracy,
                DamageDealt = stats.DamageDealt,
                DamageTaken = stats.DamageTaken,
                CalloutAssists = stats.CalloutAssists,
                VehicleDestroys = stats.VehicleDestroys,
                DriverAssists = stats.DriverAssists,
                Hijacks = stats.Hijacks,
                EmpAssists = stats.EmpAssists,
                MaxKillingSpree = stats.MaxKillingSpree,
                MedalEarned = stats.Medals.Select(m => new MedalEarned()
                {
                    MedalID = m.NameId,
                    MatchID = matchID,
                    XboxUserID = xboxUser.XUID,
                    Count = m.Count,
                    TotalPersonalScoreAwarded = m.TotalPersonalScoreAwarded,
                }).ToList(),
                
            };
        }).ToList();

        match.MatchParticipants = matchParticpants;
        await _grifballContext.Matches.AddAsync(match);
        await _grifballContext.SaveChangesAsync();
    }

    public async Task DownloadMedals()
    {
        if (await _grifballContext.Medals.AnyAsync())
        {
            throw new Exception("You already have medals!");
        }
        var client = await _haloInfiniteClientFactory.CreateAsync();

        var response = await client.Medals();

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

        var medals = response.Result.Medals.Select(medal => new Medal()
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
