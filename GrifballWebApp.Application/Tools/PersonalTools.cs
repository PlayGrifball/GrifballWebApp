using System.ComponentModel;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Matchmaking;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Tools;

/// <summary>The Late League queue: its size, and where the caller stands (null when they have no site account).</summary>
public sealed record QueueStatusDto(int PlayersInQueue, bool? YouAreInQueue, bool? YouAreInAMatch);

/// <summary>One of the caller's gamertag claims: the gamertag, how it stands, and why it was rejected if it was.</summary>
public sealed record MyClaimDto(string Gamertag, string Status, string? RejectionReason);

/// <summary>The caller's account, as /me shows it: their region, their gamertag claims and the sign-ins linked to it.</summary>
public sealed record MyAccountDto(int UserID, string? Region, IReadOnlyList<MyClaimDto> GamertagClaims, IReadOnlyList<string> SignIns);

/// <summary>
/// The caller's own things, as /queue status and /me show them: the queue, open to anyone, and their account, theirs alone (as /me's
/// replies are only for them, it is refused in a public channel).
/// </summary>
public class PersonalTools
{
    public const string NotInPublic = "Your account is yours alone, so it isn't shown in a channel: ask in a DM with the bot, or see your profile page.";

    private readonly ToolCaller _caller;
    private readonly IQueueRepository _queue;
    private readonly UserGamertagClaimService _claims;
    private readonly IApplicationDbContext _context;

    public PersonalTools(ToolCaller caller, IQueueRepository queue, UserGamertagClaimService claims, IApplicationDbContext context)
    {
        _caller = caller;
        _queue = queue;
        _claims = claims;
        _context = context;
    }

    [Description("The Late League matchmaking queue: how many players are in it, and whether you are in it or in a match (null when you have no site account).")]
    public async Task<QueueStatusDto> GetQueueStatus(CancellationToken ct = default)
    {
        if (_caller.UserID is int id && await _queue.PlayerState(id, ct) is { } you)
            return new(you.QueueCount, you.InQueue, you.InMatch);
        return new(await _queue.Count(ct), null, null);
    }

    [Description("Your own account: your region, the gamertag claims you made and how each stands, and the sign-ins linked to it (Discord, Microsoft...). "
        + "Only in a private chat (the site's, a DM with the bot).")]
    public async Task<MyAccountDto> GetMyAccount(CancellationToken ct = default)
    {
        if (_caller.InPublic)
            throw new ToolException(NotInPublic);
        var id = _caller.Me();
        var region = await _context.Users.Where(u => u.Id == id).Select(u => u.Region != null ? u.Region.RegionName : null).FirstOrDefaultAsync(ct);
        var claims = (await _claims.ClaimsOf(id, ct)).Select(c => new MyClaimDto(c.Gamertag, c.Status, c.RejectionReason)).ToList();
        var signIns = await _context.UserLogins.Where(l => l.UserId == id).OrderBy(l => l.LoginProvider)
            .Select(l => l.ProviderDisplayName ?? l.LoginProvider).ToListAsync(ct);
        return new(id, region, claims, signIns);
    }
}
