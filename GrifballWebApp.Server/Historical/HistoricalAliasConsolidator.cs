namespace GrifballWebApp.Server.Historical;

using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Proactively resolves and consolidates historical alias placeholders into canonical player accounts.
/// Runs at startup so existing legacy placeholders (e.g. "historical:Its Rainin MenZ") are cleanly merged
/// into the canonical user (e.g. "historical:MenZ" or an Xbox user with gamertag "MenZ").
/// </summary>
public static class HistoricalAliasConsolidator
{
    public static async Task<int> ConsolidateAsync(GrifballContext context, IUserMergeService userMerges, ILogger? logger = null, CancellationToken ct = default)
    {
        var mergedCount = 0;

        // Find all dummy users that match known non-canonical historical aliases
        var dummyUsers = await context.Users
            .AsNoTracking()
            .Where(u => u.IsDummyUser && u.UserName.StartsWith(HistoricalKeys.DummyUserNamePrefix))
            .Select(u => new { u.Id, u.UserName, u.DisplayName, u.XboxUserID })
            .ToListAsync(ct);

        foreach (var user in dummyUsers)
        {
            var rawTag = user.DisplayName ?? user.UserName[HistoricalKeys.DummyUserNamePrefix.Length..];
            if (!HistoricalAliases.IsKnownAlias(rawTag))
                continue;

            var canonical = HistoricalAliases.CanonicalGamertag(rawTag);
            if (rawTag.Equals(canonical, StringComparison.OrdinalIgnoreCase))
                continue;

            var canonicalDummyUserUpper = HistoricalKeys.Normalize(HistoricalKeys.DummyUserName(canonical));

            // Find canonical target account:
            // 1. A registered user with XboxUser Gamertag == canonical
            // 2. An existing dummy user historical:<canonical>
            // 3. A dummy user whose DisplayName == canonical
            var target = await context.Users
                .Include(u => u.XboxUser)
                .FirstOrDefaultAsync(u =>
                    (u.XboxUser != null && u.XboxUser.Gamertag == canonical) ||
                    u.NormalizedUserName == canonicalDummyUserUpper ||
                    (u.IsDummyUser && u.DisplayName == canonical), ct);

            if (target == null)
            {
                // No target account exists yet: load source entity and rename it to the canonical handle
                try
                {
                    var sourceEntity = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id, ct);
                    if (sourceEntity != null)
                    {
                        sourceEntity.DisplayName = canonical;
                        sourceEntity.UserName = HistoricalKeys.DummyUserName(canonical);
                        sourceEntity.NormalizedUserName = canonicalDummyUserUpper;
                        await context.SaveChangesAsync(ct);
                        logger?.LogInformation("Renamed placeholder {Source} to canonical handle {Canonical} (UserID: {UserId})", rawTag, canonical, sourceEntity.Id);
                        mergedCount++;
                    }
                }
                catch (DbUpdateException ex)
                {
                    logger?.LogWarning(ex, "Failed to rename placeholder {Source} to canonical handle {Canonical} due to constraint conflict; skipping", rawTag, canonical);
                }
                finally
                {
                    context.ChangeTracker.Clear();
                }
                continue;
            }

            if (target.Id == user.Id)
                continue;

            // Check if source still exists in database (could have been merged in earlier step)
            var sourceUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id, ct);
            if (sourceUser == null)
                continue;

            // Guard against transferring Xbox if both hold Xbox links
            if (sourceUser.XboxUserID != null && target.XboxUserID != null)
            {
                logger?.LogWarning("Skipping auto-merge for alias {Source} -> {Target}: both hold Xbox links", rawTag, canonical);
                continue;
            }

            var conflict = await userMerges.ConflictAsync(target.Id, user.Id, ct);
            if (conflict != null)
            {
                logger?.LogWarning("Skipping auto-merge for alias {Source} -> {Target}: {Reason}", rawTag, canonical, conflict);
                continue;
            }

            try
            {
                context.ChangeTracker.Clear();
                var options = MergeOptions.For(MergeTrigger.Import, $"Canonical alias consolidation: {rawTag} -> {canonical}");
                options.TransferXbox = target.XboxUserID == null;
                await userMerges.Merge(target.Id, user.Id, options, ct);
                logger?.LogInformation("Merged historical alias placeholder {Source} into {Target} (UserID: {TargetId})", rawTag, canonical, target.Id);
                mergedCount++;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to auto-merge historical alias placeholder {Source} into {Target}", rawTag, canonical);
            }
            finally
            {
                context.ChangeTracker.Clear();
            }
        }

        return mergedCount;
    }
}
