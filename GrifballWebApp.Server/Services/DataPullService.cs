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
        var foooo = await _grifballContext.Matches.ToListAsync();
        if (await _grifballContext.Matches.AnyAsync(m => m.MatchID == matchID))
        {
            _logger.LogDebug("Match {MatchID} already exists", matchID);
            return;
        }
        var client = await _haloInfiniteClientFactory.CreateAsync();

        var response = await client.StatsGetMatchStats(matchID.ToString());

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
            StartTime = response.Result.MatchInfo.StartTime,
            EndTime = response.Result.MatchInfo.EndTime,
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

            return new MatchParticipant()
            {
                XboxUser = xboxUser,
            };
        }).ToList();

        match.MatchParticipants = matchParticpants;
        await _grifballContext.AddAsync(match);
        await _grifballContext.SaveChangesAsync();
    }
}
