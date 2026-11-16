using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Voice;

/// <summary>
/// Who the voice chat may hear: the Discord accounts that opted in (<see cref="DiscordUser.VoiceOptIn"/>). Everyone else's
/// audio is dropped before it is transcribed.
/// </summary>
public sealed class VoiceConsents(IApplicationDbContext context)
{
    /// <summary>
    /// <paramref name="discordUserId"/> opted in to being heard; an account the bot hasn't seen is added, as Discord gave it
    /// (<paramref name="username"/>).
    /// </summary>
    public async Task OptInAsync(ulong discordUserId, string? username, CancellationToken ct = default)
    {
        var id = (long)discordUserId;
        var account = await context.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordUserID == id, ct);
        if (account is null)
        {
            account = DiscordUser.FromDiscord(id, username);
            context.DiscordUsers.Add(account);
        }
        account.OptInToVoice();
        await context.SaveChangesAsync(ct);
    }

    /// <summary><paramref name="discordUserId"/> opted out: no longer heard. An account the bot hasn't seen was never heard.</summary>
    public async Task OptOutAsync(ulong discordUserId, CancellationToken ct = default)
    {
        var id = (long)discordUserId;
        if (await context.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordUserID == id, ct) is not { } account)
            return;
        account.OptOutOfVoice();
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Which of <paramref name="discordUserIds"/> opted in.</summary>
    public async Task<IReadOnlySet<ulong>> OptedInAsync(IEnumerable<ulong> discordUserIds, CancellationToken ct = default)
    {
        var ids = discordUserIds.Select(id => (long)id).ToList();
        var heard = await context.DiscordUsers.AsNoTracking()
            .Where(d => ids.Contains(d.DiscordUserID) && d.VoiceOptIn)
            .Select(d => d.DiscordUserID)
            .ToListAsync(ct);
        return heard.Select(id => (ulong)id).ToHashSet();
    }
}
