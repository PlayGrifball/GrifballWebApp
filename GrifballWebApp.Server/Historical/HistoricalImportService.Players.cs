using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

/// <summary>The players lists (<see cref="HistoricalArchiveInput.Players"/>): Discord IDs and regions.</summary>
public partial class HistoricalImportService
{
    /// <summary>
    /// Each player's Discord ID and region, in one transaction after the seasons:
    /// <list type="bullet">
    /// <item>No site user has the Discord ID: it is linked to the gamertag's placeholder (made if need be), so signing in
    /// with Discord later merges the placeholder into the new account (DiscordAccountLinkService) instead of making a second.</item>
    /// <item>A signed-up user has it: the gamertag's placeholder is merged into them (audited, DiscordMatch) when the gamertag
    /// fits them (their Xbox account has or had it, or they have none); otherwise a pending DiscordMatch claim.</item>
    /// <item>A Discord ID given for more than one gamertag never links or merges: claims where its user is known, and a warning.</item>
    /// </list>
    /// A region fills in a user or placeholder with none (RegionSource Import). Running it again changes nothing.
    /// </summary>
    private async Task ImportPlayers(List<ArchivePlayerInput> players, HistoricalImportReport report, CancellationToken ct)
    {
        var counts = new HistoricalImportReport();
        await _context.StartTransactionAsync(ct);
        try
        {
            var rows = players.Where(p => !string.IsNullOrWhiteSpace(p.Gamertag)).ToList();
            var conflicting = rows.Where(p => p.DiscordUserId is not null)
                .GroupBy(p => p.DiscordUserId!.Value)
                .Where(g => g.Select(p => HistoricalKeys.LooseKey(p.Gamertag)).Distinct().Count() > 1)
                .ToDictionary(g => g.Key, g => g.Select(p => HistoricalKeys.CleanGamertag(p.Gamertag)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
            foreach (var (discordId, tags) in conflicting)
                Warn(counts, $"Players lists: Discord ID {discordId} is given for more than one gamertag ({string.Join(", ", tags)}); nothing linked or merged automatically");

            foreach (var player in rows)
            {
                var userId = await ImportPlayer(player, player.DiscordUserId is { } id && conflicting.ContainsKey(id), counts, ct);
                if (userId is not null)
                    await FillRegion(userId.Value, player.Region, counts, ct);
            }
            await _context.SaveChangesAsync(ct);
            await _context.CommitTransactionAsync(ct);
            AddPlayers(report, counts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _context.RollbackTransactionAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Historical import of the players lists failed and was rolled back");
            report.Errors.Add($"Players lists: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>One player's Discord ID. Returns the user the row is about now (for its region), or null.</summary>
    private async Task<int?> ImportPlayer(ArchivePlayerInput player, bool conflicting, HistoricalImportReport counts, CancellationToken ct)
    {
        var gamertag = HistoricalKeys.CleanGamertag(player.Gamertag);
        var placeholder = await _matching.PlaceholderAsync(gamertag, untiedOnly: false, ct);
        var real = await _matching.RealUserOfAsync(gamertag, ct);
        var discordId = player.DiscordUserId;
        if (discordId is null)
            return real?.Id ?? placeholder?.Id;

        var holder = await _context.Users.FirstOrDefaultAsync(u => u.DiscordUserID == discordId, ct);
        if (holder is not null && holder.IsDummyUser)
        {
            // Linked by an earlier run (nothing to do), or the lists give the ID to two players' placeholders
            if (placeholder is not null && placeholder.Id != holder.Id)
                Warn(counts, $"Players lists: Discord ID {discordId} of {gamertag} is linked to the placeholder {holder.UserName}; left as it is");
            return placeholder?.Id ?? holder.Id;
        }

        if (holder is null)
        {
            if (conflicting)
                return real?.Id ?? placeholder?.Id;
            if (real is not null)
            {
                // A signed-up user's own sign-in links their Discord: a list doesn't decide it for them
                Warn(counts, $"Players lists: {gamertag} (Discord ID {discordId}) is {real.UserName}'s gamertag; their Discord is linked when they sign in with it");
                return real.Id;
            }
            if (placeholder is null)
            {
                var (made, created) = await _matching.CreatePlaceholderAsync(gamertag, null, ct);
                placeholder = made;
                if (created)
                    counts.DummyUsersCreated++;
            }
            if (placeholder.DiscordUserID is not null)
            {
                Warn(counts, $"Players lists: {gamertag}'s placeholder has Discord ID {placeholder.DiscordUserID}, the list says {discordId}; left as it is");
                return placeholder.Id;
            }
            if (!await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == discordId, ct))
            {
                var name = string.IsNullOrWhiteSpace(player.DiscordUsername) ? discordId.Value.ToString() : player.DiscordUsername.Trim();
                _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = discordId.Value, DiscordUsername = name });
            }
            placeholder.LinkDiscord(discordId.Value);
            await _context.SaveChangesAsync(ct);
            counts.DiscordLinked++;
            return placeholder.Id;
        }

        // A signed-up user has the Discord ID
        if (placeholder is null || placeholder.Id == holder.Id || !placeholder.IsDummyUser)
            return holder.Id;
        if (placeholder.XboxUserID is not null && holder.XboxUserID is not null)
        {
            Warn(counts, $"Players lists: {gamertag}'s placeholder is another Xbox account's than {holder.UserName}'s (Discord ID {discordId}); left for a sysadmin");
            return holder.Id;
        }
        if (!conflicting && (holder.XboxUserID is null || await KnownGamertagOf(holder, gamertag, ct)))
        {
            try
            {
                await _merges.Merge(holder.Id, placeholder.Id, MergeOptions.For(MergeTrigger.DiscordMatch, "Discord ID match"), ct);
                counts.DiscordMerged++;
                return holder.Id;
            }
            catch (MergeConflictException e)
            {
                Warn(counts, $"Players lists: {gamertag}'s placeholder can't be merged into {holder.UserName} (Discord ID {discordId}): {e.Message}");
            }
        }
        if (placeholder.XboxUserID is null
            && await _matching.ClaimForUserAsync(GamertagClaimKind.DiscordMatch, holder.Id, holder.XboxUserID, gamertag, placeholder.Id, evenIfRejected: false, ct) is not null)
        {
            counts.DiscordClaimsCreated++;
            await _context.SaveChangesAsync(ct);
        }
        return holder.Id;
    }

    /// <summary>Whether the user's Xbox account has or had the gamertag (compared loosely).</summary>
    private async Task<bool> KnownGamertagOf(User user, string gamertag, CancellationToken ct)
    {
        var key = HistoricalKeys.LooseKey(gamertag);
        var current = await _context.XboxUsers.Where(x => x.XboxUserID == user.XboxUserID).Select(x => x.Gamertag).FirstOrDefaultAsync(ct);
        if (current is not null && HistoricalKeys.LooseKey(current) == key)
            return true;
        var past = await _context.PastGamertags().Where(h => h.XboxUserID == user.XboxUserID).Select(h => h.Gamertag).ToListAsync(ct);
        return past.Any(p => HistoricalKeys.LooseKey(p) == key);
    }

    /// <summary>The list's region, for a user with none (or one an earlier import set): never over one the user or a sysadmin chose.</summary>
    private async Task FillRegion(int userId, string? region, HistoricalImportReport counts, CancellationToken ct)
    {
        var regionId = Regions.Normalize(region);
        if (regionId is null)
            return;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.ChooseRegion(regionId, RegionSource.Import) is not RegionChoice.Set)
            return;
        await _context.SaveChangesAsync(ct);
        counts.RegionsSet++;
    }

    private static void AddPlayers(HistoricalImportReport report, HistoricalImportReport players)
    {
        report.DiscordLinked += players.DiscordLinked;
        report.DiscordMerged += players.DiscordMerged;
        report.DiscordClaimsCreated += players.DiscordClaimsCreated;
        report.RegionsSet += players.RegionsSet;
        report.DummyUsersCreated += players.DummyUsersCreated;
        report.Warnings.AddRange(players.Warnings);
    }
}
