using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Voice;

/// <summary>
/// Who the voice chat may hear: the Discord accounts that opted in (<see cref="VoiceConsent"/>, a row each). Everyone else's
/// audio is dropped before it is transcribed.
/// </summary>
public sealed class VoiceConsents(IApplicationDbContext context, TimeProvider time)
{
    /// <summary><paramref name="discordUserId"/> opted in to being heard; given already, it stands as it was.</summary>
    public async Task OptInAsync(ulong discordUserId, CancellationToken ct = default)
    {
        var id = (long)discordUserId;
        if (await context.VoiceConsents.AnyAsync(v => v.DiscordUserID == id, ct))
            return;
        context.VoiceConsents.Add(VoiceConsent.Give(id, time.GetUtcNow().UtcDateTime));
        await context.SaveChangesAsync(ct);
    }

    /// <summary><paramref name="discordUserId"/> opted out: no longer heard. With no consent, nothing to remove.</summary>
    public async Task OptOutAsync(ulong discordUserId, CancellationToken ct = default)
    {
        var id = (long)discordUserId;
        if (await context.VoiceConsents.SingleOrDefaultAsync(v => v.DiscordUserID == id, ct) is not { } consent)
            return;
        context.VoiceConsents.Remove(consent);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Which of <paramref name="discordUserIds"/> opted in.</summary>
    public async Task<IReadOnlySet<ulong>> OptedInAsync(IEnumerable<ulong> discordUserIds, CancellationToken ct = default)
    {
        var ids = discordUserIds.Select(id => (long)id).ToList();
        var heard = await context.VoiceConsents.AsNoTracking()
            .Where(v => ids.Contains(v.DiscordUserID))
            .Select(v => v.DiscordUserID)
            .ToListAsync(ct);
        return heard.Select(id => (ulong)id).ToHashSet();
    }
}
