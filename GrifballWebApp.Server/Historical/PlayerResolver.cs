using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Finds the user a historical gamertag belongs to, creating one placeholder (dummy) user when nobody has it, so a
/// player is one user across every season and title of the archive, and across re-runs. In order:
/// <list type="number">
/// <item>The Xbox account (by XUID) when the row comes from a Waypoint match, if it is linked to a user.</item>
/// <item>The user already holding historical rows recorded under that gamertag (PlayerSeasonStats.Gamertag,
/// PlayerChampionships.GamertagUsed). This is how a placeholder is found again, and it keeps finding the right user
/// after a commissioner merged the placeholder into a real account: the rows moved with the merge.</item>
/// <item>A real account whose Xbox account (XboxUser) has that gamertag now.</item>
/// <item>A PlayerAlias with it: a genuine second Xbox account of a user (aliases are never renames).</item>
/// <item>The placeholder by its user name, <see cref="HistoricalKeys.DummyUserNamePrefix"/> + gamertag.</item>
/// </list>
/// Otherwise it creates the placeholder: IsDummyUser, UserName "historical:{gamertag}" (unique, as NormalizedUserName is,
/// and a name no sign-up can take), DisplayName the gamertag, linked to the Xbox account when the row has one
/// that no user holds, so the player claims it by setting their gamertag (SetGamertagService merges the placeholder).
/// </summary>
internal sealed class PlayerResolver
{
    private readonly GrifballContext _context;
    private readonly Dictionary<string, int> _byGamertag = new();

    public PlayerResolver(GrifballContext context) => _context = context;

    public int DummiesCreated { get; private set; }

    public async Task<int> Resolve(string rawGamertag, long? xboxUserId, CancellationToken ct)
    {
        var gamertag = HistoricalKeys.CleanGamertag(rawGamertag);
        var normalized = HistoricalKeys.Normalize(gamertag);
        XboxUser? xboxUser = null;
        if (xboxUserId is not null)
        {
            xboxUser = await _context.XboxUsers.Include(x => x.User).FirstOrDefaultAsync(x => x.XboxUserID == xboxUserId, ct);
            if (xboxUser?.User is not null)
                return _byGamertag[normalized] = xboxUser.User.Id;
        }

        if (!_byGamertag.TryGetValue(normalized, out var userId))
        {
            userId = await Find(gamertag, normalized, ct) ?? 0;
            if (userId == 0)
                userId = await CreateDummy(gamertag, xboxUser, ct);
            _byGamertag[normalized] = userId;
        }

        // A placeholder found by its gamertag gets the Xbox account a Waypoint match shows it played on
        if (xboxUser is { User: null })
        {
            var user = await _context.Users.FirstAsync(u => u.Id == userId, ct);
            if (user.IsDummyUser && user.XboxUserID is null)
            {
                user.XboxUserID = xboxUser.XboxUserID;
                await _context.SaveChangesAsync(ct);
            }
        }
        return userId;
    }

    private async Task<int?> Find(string gamertag, string normalized, CancellationToken ct)
    {
        // Text compares case-insensitively on both databases
        var fromStats = await _context.PlayerSeasonStats.Where(s => s.Gamertag == gamertag)
            .OrderBy(s => s.PlayerSeasonStatID).Select(s => (int?)s.UserID).FirstOrDefaultAsync(ct);
        if (fromStats is not null)
            return fromStats;

        var fromTitles = await _context.PlayerChampionships.Where(c => c.GamertagUsed == gamertag)
            .OrderBy(c => c.PlayerChampionshipID).Select(c => (int?)c.UserID).FirstOrDefaultAsync(ct);
        if (fromTitles is not null)
            return fromTitles;

        var fromXbox = await _context.Users.Where(u => u.XboxUser != null && u.XboxUser.Gamertag == gamertag)
            .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
        if (fromXbox is not null)
            return fromXbox;

        var fromAlias = await _context.PlayerAliases.Where(a => a.NormalizedAlias == normalized)
            .OrderBy(a => a.PlayerAliasID).Select(a => (int?)a.UserID).FirstOrDefaultAsync(ct);
        if (fromAlias is not null)
            return fromAlias;

        var dummyName = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant();
        return await _context.Users.Where(u => u.NormalizedUserName == dummyName)
            .Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
    }

    private async Task<int> CreateDummy(string gamertag, XboxUser? xboxUser, CancellationToken ct)
    {
        var userName = HistoricalKeys.DummyUserName(gamertag);
        var user = new User
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = gamertag.Length > 30 ? gamertag[..30] : gamertag,
            IsDummyUser = true,
            XboxUserID = xboxUser is { User: null } ? xboxUser.XboxUserID : null,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
        DummiesCreated++;
        return user.Id;
    }
}
