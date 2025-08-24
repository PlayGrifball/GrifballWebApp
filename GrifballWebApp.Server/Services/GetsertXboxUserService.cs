using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Services;

public interface IGetsertXboxUserService
{
    Task<(XboxUser?, string?)> GetsertXboxUserByGamertag(string gamertag, CancellationToken ct = default);
    Task<XboxUser> GetsertXboxUserByXuid(long xuid, CancellationToken ct = default);
    Task<XboxUser[]> GetsertXboxUsersByXuid(long[] xuid, CancellationToken ct = default);
}

public class GetsertXboxUserService : IGetsertXboxUserService
{
    private readonly ILogger<GetsertXboxUserService> _logger;
    private readonly GrifballContext _grifballContext;
    private readonly IHaloInfiniteClientFactory _infiniteClientFactory;
    public GetsertXboxUserService(ILogger<GetsertXboxUserService> logger, GrifballContext context, IHaloInfiniteClientFactory factory)
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

        var response = await _infiniteClientFactory.UserByGamertag(gamertag);

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

    public async Task<XboxUser[]> GetsertXboxUsersByXuid(long[] xuid, CancellationToken ct = default)
    {
        var existing = await _grifballContext.XboxUsers
            .Where(x => xuid.Contains(x.XboxUserID))
            .ToArrayAsync(ct);

        var missing = xuid
            .Where(x => !existing.Select(x => x.XboxUserID).Contains(x))
            .ToArray();

        if (missing.Any() is false)
            return existing;

        // Prep the XUIDs as strings for the API call
        var xuidStr = xuid
            .Except(existing.Select(x => x.XboxUserID)) // But do not query for existing users.
            .Select(x => x.ToString());
        var users = await _infiniteClientFactory.Users(xuidStr);

        if (users.Result is null)
            throw new Exception("Failed to resolved gamertag: " + users.Error.Message);

        var stillMissing = missing
            .Where(x => !users.Result.Any(u => u.xuid == x.ToString()))
            .ToArray();

        if (stillMissing.Any())
            throw new Exception("Failed to find the following xbox users: " + string.Join(',', stillMissing));

        var newXboxUsers = new List<XboxUser>();
        foreach (var user in users.Result)
        {
            var newXboxUser = new XboxUser()
            {
                XboxUserID = long.Parse(user.xuid),
                Gamertag = user.gamertag,
            };
            _grifballContext.XboxUsers.Add(newXboxUser);
            newXboxUsers.Add(newXboxUser);
        }
        
        await _grifballContext.SaveChangesAsync(ct);

        return existing.Concat(newXboxUsers).ToArray();
    }

    public async Task<XboxUser> GetsertXboxUserByXuid(long xuid, CancellationToken ct = default)
    {
        var xboxUser = await _grifballContext.XboxUsers
            .Where(x => x.XboxUserID == xuid)
            .FirstOrDefaultAsync(ct);

        if (xboxUser is not null)
            return xboxUser;

        var xuidStr = xuid.ToString();//.AddXUIDWrapper();
        var users = await _infiniteClientFactory.Users([xuidStr]);

        if (users.Result is null)
            throw new Exception("Failed to resolved gamertag: " + users.Error.Message);
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
