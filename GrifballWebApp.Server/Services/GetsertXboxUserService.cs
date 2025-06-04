using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Services;

public class GetsertXboxUserService
{
    private readonly ILogger<GetsertXboxUserService> _logger;
    private readonly GrifballContext _grifballContext;
    private readonly HaloInfiniteClientFactory _infiniteClientFactory;
    public GetsertXboxUserService(ILogger<GetsertXboxUserService> logger, GrifballContext context, HaloInfiniteClientFactory factory)
    {
        _logger = logger;
        _grifballContext = context;
        _infiniteClientFactory = factory;
    }

    public async Task<(XboxUser?, string?)> GetsertXboxUserByGamertag(string gamertag, CancellationToken ct = default)
    {
        var xboxUser = await _grifballContext.XboxUsers
            .Include(x => x.User) // Include user so we know if the gamertag is already associated with a user
            .Where(x => x.Gamertag == gamertag)
            .FirstOrDefaultAsync(ct);

        if (xboxUser is not null)
            return (xboxUser, null);

        var client = await _infiniteClientFactory.CreateAsync();

        var response = await client.UserByGamertag(gamertag);

        if (response is null || response.Result is null || string.IsNullOrEmpty(response.Result.gamertag))
        {
            return (null, "Did not find that gamertag");
        }

        var parsed = long.TryParse(response.Result.xuid, out var xboxUserID);

        if (parsed is false)
            return (null, "Failed to parse xuid. Contact sysadmin");

        var newXboxUser = new XboxUser()
        {
            XboxUserID = xboxUserID,
            Gamertag = response.Result.gamertag,
        };

        _grifballContext.XboxUsers.Add(newXboxUser);
        await _grifballContext.SaveChangesAsync(ct);

        return (newXboxUser, null);
    }


    public async Task<XboxUser> GetsertXboxUserByXuid(long xuid, CancellationToken ct = default)
    {
        var xboxUser = await _grifballContext.XboxUsers
            .Where(x => x.XboxUserID == xuid)
            .FirstOrDefaultAsync(ct);

        if (xboxUser is not null)
            return xboxUser;

        var client = await _infiniteClientFactory.CreateAsync();

        var xuidStr = xuid.ToString().AddXUIDWrapper();
        var users = await client.Users([xuidStr]);

        var user = users.Result.FirstOrDefault(u => u.xuid == xuidStr);
        if (user is null)
        {
            _logger.LogError("Gamertag {XUID} not found", xuid);
            throw new Exception("Failed to find user");
        }

        var newXboxUser = new XboxUser()
        {
            XboxUserID = xuid,
            Gamertag = user.gamertag,
        };

        _grifballContext.XboxUsers.Add(newXboxUser);
        await _grifballContext.SaveChangesAsync(ct);

        return newXboxUser;
    }
}
