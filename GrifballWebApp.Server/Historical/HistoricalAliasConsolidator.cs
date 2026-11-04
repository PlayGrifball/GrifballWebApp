namespace GrifballWebApp.Server.Historical;

using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>What a consolidation run did: a line per merge made, and per merge that failed.</summary>
public sealed class HistoricalAliasConsolidation
{
    public List<string> Merged { get; } = [];
    public List<string> Failed { get; } = [];
}

/// <summary>
/// Merges the placeholders of known historical aliases (e.g. "historical:Its Rainin MenZ") into their canonical player
/// (<see cref="HistoricalAliases"/>). Run by the historical import worker (<see cref="HistoricalImportRunner"/>) after each
/// import and each refresh of the sources, so it never runs during an import.
/// <list type="bullet">
/// <item>The target: the one account whose Xbox gamertag is the canonical one and whose gamertag history has one of its
/// aliases; else the canonical placeholder ("historical:MenZ"); else a canonical placeholder made for it. Two accounts
/// that could be it: none is chosen.</item>
/// <item>Every change is a merge (<see cref="IUserMergeService.Merge"/>): audited in UserMerges, and a sysadmin can undo it.
/// A merge of the placeholder that was undone is never made again.</item>
/// <item>A failed merge changes nothing (it rolls back), is reported, and the run goes on with the next placeholder.</item>
/// </list>
/// </summary>
public static class HistoricalAliasConsolidator
{
    /// <summary>The user names (normalised) of the aliases' placeholders: the registry is fixed, so these are too.</summary>
    private static readonly List<string> PlaceholderNames = HistoricalAliases.AllAliases
        .Select(a => HistoricalKeys.DummyUserName(a).ToUpperInvariant()).Distinct().ToList();

    public static async Task<HistoricalAliasConsolidation> ConsolidateAsync(GrifballContext context, IUserMergeService userMerges, ILogger? logger = null, CancellationToken ct = default)
    {
        logger ??= NullLogger.Instance;
        var result = new HistoricalAliasConsolidation();
        // A caller's own transaction: a failure inside it rolls all of it back, so the run stops there
        var callerTransaction = context.InTransaction;

        // Only the placeholders named after an alias are loaded
        var placeholders = await context.Users.AsNoTracking()
            .Where(u => u.IsDummyUser && PlaceholderNames.Contains(u.NormalizedUserName!))
            .OrderBy(u => u.Id)
            .Select(u => new Account(u.Id, u.XboxUserID, u.UserName))
            .ToListAsync(ct);
        if (placeholders.Count == 0)
            return result;

        // A merge a sysadmin undid stays undone
        var ids = placeholders.Select(p => p.Id).ToList();
        var undone = (await context.UserMerges.AsNoTracking()
            .Where(m => m.UndoneAt != null && ids.Contains(m.SourceUserID))
            .Select(m => m.SourceUserID)
            .ToListAsync(ct)).ToHashSet();

        foreach (var placeholder in placeholders)
        {
            var rawTag = placeholder.UserName![HistoricalKeys.DummyUserNamePrefix.Length..];
            var canonical = HistoricalAliases.CanonicalGamertag(rawTag);
            if (undone.Contains(placeholder.Id))
            {
                logger.LogInformation("Not merging placeholder {Source} into {Canonical} again: a sysadmin undid that merge", rawTag, canonical);
                continue;
            }

            try
            {
                var merged = await ConsolidateOne(context, userMerges, placeholder, rawTag, canonical, logger, ct);
                if (merged is not null)
                {
                    result.Merged.Add(merged);
                    logger.LogInformation("Alias consolidation: {Merge}", merged);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed to merge historical alias placeholder {Source} into {Canonical}", rawTag, canonical);
                result.Failed.Add($"{rawTag} into {canonical}: {ex.GetBaseException().Message}");
                context.ChangeTracker.Clear();
                if (context.InTransaction)
                {
                    if (callerTransaction)
                    {
                        // Its transaction was rolled back with the merge: nothing more can be saved in it
                        result.Failed.Add("Stopped: the transaction it ran in was rolled back");
                        break;
                    }
                    // A merge that left its transaction open: rolled back, so the next ones commit on their own
                    while (context.InTransaction)
                        await context.RollbackTransactionAsync(CancellationToken.None);
                }
            }
        }

        return result;
    }

    /// <summary>Merges one alias placeholder into its canonical player. Returns what it did, or null when it left it.</summary>
    private static async Task<string?> ConsolidateOne(GrifballContext context, IUserMergeService userMerges, Account source, string rawTag, string canonical, ILogger logger, CancellationToken ct)
    {
        var sourceId = source.Id;
        // 1. The account that holds the canonical gamertag, only when its own history shows it had one of the aliases: a
        // stored gamertag can be stale, and a short one reused by someone else. Two such accounts: left alone.
        var aliases = HistoricalAliases.GetAliasesFor(canonical).Select(HistoricalKeys.Normalize).ToList();
        var holders = await context.Users.AsNoTracking()
            .Where(u => u.XboxUser != null && u.XboxUser.Gamertag == canonical)
            .OrderBy(u => u.Id)
            .Select(u => new Account(u.Id, u.XboxUserID, u.UserName))
            .ToListAsync(ct);
        var evidenced = new List<Account>();
        foreach (var holder in holders)
        {
            if (await context.XboxGamertagHistory.AnyAsync(h => h.XboxUserID == holder.XboxUserID && aliases.Contains(h.NormalizedGamertag), ct))
                evidenced.Add(holder);
        }
        if (evidenced.Count > 1)
        {
            logger.LogWarning("Not merging placeholder {Source}: {Count} accounts hold {Canonical} and had one of its aliases", rawTag, evidenced.Count, canonical);
            return null;
        }

        var target = evidenced.Count == 1 ? evidenced[0] : null;

        // 2. The canonical placeholder
        if (target is null)
        {
            var canonicalName = HistoricalKeys.DummyUserName(canonical).ToUpperInvariant();
            target = await context.Users.AsNoTracking()
                .Where(u => u.IsDummyUser && u.NormalizedUserName == canonicalName)
                .Select(u => new Account(u.Id, u.XboxUserID, u.UserName))
                .FirstOrDefaultAsync(ct);
        }

        if (target is { } found)
        {
            // The alias placeholder is itself the account that holds the canonical gamertag
            if (found.Id == sourceId)
                return null;
            if (source.XboxUserID is not null && found.XboxUserID is not null)
            {
                logger.LogWarning("Not merging placeholder {Source} into {Target}: both hold Xbox accounts", rawTag, found.UserName);
                return null;
            }
            var conflict = await userMerges.ConflictAsync(found.Id, sourceId, ct);
            if (conflict is not null)
            {
                logger.LogWarning("Not merging placeholder {Source} into {Target}: {Reason}", rawTag, found.UserName, conflict);
                return null;
            }
            context.ChangeTracker.Clear();
            await userMerges.Merge(found.Id, sourceId, Options(rawTag, canonical, source.XboxUserID is not null), ct);
            return $"merged {rawTag} into {found.UserName} (user {found.Id})";
        }

        // 3. No account to merge into: the canonical placeholder is made, and the alias's merged into it, in one
        // transaction (so a failed merge leaves no empty placeholder behind). Step 2 found no placeholder of that name, so
        // what this makes (or finds by a #n suffix) is tied to no Xbox account and takes the alias placeholder's
        context.ChangeTracker.Clear();
        await context.StartTransactionAsync(ct);
        try
        {
            var (made, created) = await new GamertagMatching(context).CreatePlaceholderAsync(canonical, null, ct);
            await userMerges.Merge(made.Id, sourceId, Options(rawTag, canonical, source.XboxUserID is not null), ct);
            await context.CommitTransactionAsync(ct);
            return $"merged {rawTag} into {(created ? "new placeholder " : "")}{made.UserName} (user {made.Id})";
        }
        catch
        {
            if (context.InTransaction)
                await context.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    internal sealed record Account(int Id, long? XboxUserID, string? UserName);

    private static MergeOptions Options(string rawTag, string canonical, bool transferXbox)
    {
        var options = MergeOptions.For(MergeTrigger.Import, $"Canonical alias consolidation: {rawTag} -> {canonical}");
        options.TransferXbox = transferXbox;
        return options;
    }
}
