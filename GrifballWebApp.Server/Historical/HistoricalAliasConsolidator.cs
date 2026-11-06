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
/// aliases; else the canonical placeholder ("historical:MenZ"), when it is tied to no Xbox account or to one whose history
/// has one of the aliases too; else a canonical placeholder made for it. Two accounts that could be it: none is
/// chosen.</item>
/// <item>A placeholder the import kept apart from its handle's other player (two gamertags of the handle in one of a
/// season's lists: <see cref="PlayerResolver.SharedAliases"/>) is a different player, and is left.</item>
/// <item>Every change is a merge (<see cref="IUserMergeService.Merge"/>): audited in UserMerges, and a sysadmin can undo it.
/// A consolidation of the placeholder into the same canonical player that was undone is never made again.</item>
/// <item>A failed merge changes nothing (it rolls back), is reported, and the run goes on with the next placeholder. In a
/// caller's transaction the run stops there instead, and the merges the rollback undid are reported as such.</item>
/// </list>
/// </summary>
public static class HistoricalAliasConsolidator
{
    /// <summary>The user names (normalised) of the aliases' placeholders: the registry is fixed, so these are too.</summary>
    private static readonly List<string> PlaceholderNames = HistoricalAliases.AllAliases
        .Select(a => HistoricalKeys.DummyUserName(a).ToUpperInvariant()).Distinct().ToList();

    private const string ReasonPrefix = "Canonical alias consolidation: ";

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

        // A consolidation a sysadmin undid stays undone: only this step's own merges (by their reason) count, not another
        // merge of the placeholder that was undone
        var ids = placeholders.Select(p => p.Id).ToList();
        var undone = (await context.UserMerges.AsNoTracking()
            .Where(m => m.UndoneAt != null && m.Trigger == MergeTrigger.Import && ids.Contains(m.SourceUserID) && m.Reason != null && m.Reason.StartsWith(ReasonPrefix))
            .Select(m => new { m.SourceUserID, m.Reason })
            .ToListAsync(ct))
            .Select(m => (m.SourceUserID, m.Reason!))
            .ToHashSet();

        foreach (var placeholder in placeholders)
        {
            var rawTag = placeholder.UserName![HistoricalKeys.DummyUserNamePrefix.Length..];
            var canonical = HistoricalAliases.CanonicalGamertag(rawTag);
            if (undone.Contains((placeholder.Id, Reason(rawTag, canonical))))
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
                if (callerTransaction)
                {
                    if (context.RolledBackInside)
                    {
                        // The caller's transaction was rolled back with the merge, and the run's earlier merges with it
                        result.Failed.AddRange(result.Merged.Select(m => $"Rolled back: {m}"));
                        result.Merged.Clear();
                        result.Failed.Add("Stopped: the transaction it ran in was rolled back");
                    }
                    else
                    {
                        result.Failed.Add("Stopped: it runs in the caller's transaction, which decides what to do with the failure");
                    }
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>Merges one alias placeholder into its canonical player. Returns what it did, or null when it left it.</summary>
    private static async Task<string?> ConsolidateOne(GrifballContext context, IUserMergeService userMerges, Account source, string rawTag, string canonical, ILogger logger, CancellationToken ct)
    {
        var sourceId = source.Id;
        if (await KeptApartAsync(context, sourceId, rawTag, canonical, ct))
        {
            logger.LogInformation("Not merging placeholder {Source} into {Canonical}: the import kept them apart, as two players of one season's list", rawTag, canonical);
            return null;
        }

        var matching = new GamertagMatching(context);
        // 1. The account that holds the canonical gamertag, only when its own history shows it had one of the aliases: a
        // stored gamertag can be stale, and a short one reused by someone else. Two such accounts: left alone.
        var aliases = HistoricalAliases.GetAliasesFor(canonical).Select(HistoricalKeys.Normalize)
            .Append(HistoricalKeys.Normalize(rawTag)).Distinct().ToList();
        var evidenced = await matching.HoldersAsync(canonical, aliases, ct);
        if (evidenced.Count > 1)
        {
            logger.LogWarning("Not merging placeholder {Source}: {Count} accounts hold {Canonical} and had one of its aliases", rawTag, evidenced.Count, canonical);
            return null;
        }

        var target = evidenced.Count == 1 ? new Account(evidenced[0].UserId, evidenced[0].XboxUserID, evidenced[0].UserName) : null;

        // 2. The canonical placeholder, under the same rule: one tied to an Xbox account (a Waypoint opponent's, a
        // sign-up's to be) only when that account's history has one of the aliases
        if (target is null)
        {
            var canonicalName = HistoricalKeys.DummyUserName(canonical).ToUpperInvariant();
            target = await context.Users.AsNoTracking()
                .Where(u => u.IsDummyUser && u.NormalizedUserName == canonicalName)
                .Select(u => new Account(u.Id, u.XboxUserID, u.UserName))
                .FirstOrDefaultAsync(ct);
            if (target is { XboxUserID: { } tiedTo } && target.Id != sourceId && !await matching.HistoryHasAnyAsync(tiedTo, aliases, ct))
            {
                logger.LogWarning("Not merging placeholder {Source} into {Target}: it is tied to an Xbox account whose history has none of the aliases", rawTag, target.UserName);
                return null;
            }
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
            var (made, created) = await matching.CreatePlaceholderAsync(canonical, null, ct);
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

    /// <summary>
    /// Whether the import kept the placeholder apart from its handle's other player: in one of a season's lists (its
    /// sheet rows for a phase, a tier's title roster, a Late League season's rows, a recorded game) the placeholder is
    /// there, and another user is too under another gamertag of the same handle. The import resolves two such gamertags
    /// each by its own (<see cref="PlayerResolver.SharedAliases"/>): they are two players, whatever the registry says.
    /// </summary>
    private static async Task<bool> KeptApartAsync(GrifballContext context, int placeholderId, string rawTag, string canonical, CancellationToken ct)
    {
        var own = HistoricalKeys.Normalize(rawTag);
        var handle = HistoricalKeys.Normalize(canonical);
        bool OtherOfTheHandle(string? tag) => !string.IsNullOrWhiteSpace(tag)
            && HistoricalKeys.Normalize(tag) != own
            && HistoricalKeys.Normalize(HistoricalAliases.CanonicalGamertag(tag)) == handle;

        var phases = await context.PlayerSeasonStats.AsNoTracking().Where(s => s.UserID == placeholderId)
            .Select(s => new { s.SeasonID, s.IsPlayoffs, s.IsCombined }).Distinct().ToListAsync(ct);
        if (phases.Count > 0)
        {
            var seasons = phases.Select(p => p.SeasonID).Distinct().ToList();
            var others = await context.PlayerSeasonStats.AsNoTracking().Where(s => s.UserID != placeholderId && seasons.Contains(s.SeasonID))
                .Select(s => new { s.SeasonID, s.IsPlayoffs, s.IsCombined, s.Gamertag }).ToListAsync(ct);
            if (others.Any(o => phases.Contains(new { o.SeasonID, o.IsPlayoffs, o.IsCombined }) && OtherOfTheHandle(o.Gamertag)))
                return true;
        }

        var tiers = await context.PlayerChampionships.AsNoTracking().Where(c => c.UserID == placeholderId)
            .Select(c => new { c.SeasonID, c.Tier }).Distinct().ToListAsync(ct);
        if (tiers.Count > 0)
        {
            var seasons = tiers.Select(t => t.SeasonID).Distinct().ToList();
            var others = await context.PlayerChampionships.AsNoTracking().Where(c => c.UserID != placeholderId && seasons.Contains(c.SeasonID))
                .Select(c => new { c.SeasonID, c.Tier, c.GamertagUsed }).ToListAsync(ct);
            if (others.Any(o => tiers.Contains(new { o.SeasonID, o.Tier }) && OtherOfTheHandle(o.GamertagUsed)))
                return true;
        }

        var lateLeague = await context.MatchmakingPlayerStats.AsNoTracking().Where(s => s.UserID == placeholderId)
            .Select(s => s.MatchmakingSeasonID).Distinct().ToListAsync(ct);
        if (lateLeague.Count > 0)
        {
            var others = await context.MatchmakingPlayerStats.AsNoTracking().Where(s => s.UserID != placeholderId && lateLeague.Contains(s.MatchmakingSeasonID))
                .Select(s => s.Gamertag).ToListAsync(ct);
            if (others.Any(OtherOfTheHandle))
                return true;
        }

        var games = await context.RecordedGameParticipants.AsNoTracking().Where(p => p.UserID == placeholderId)
            .Select(p => p.RecordedGameID).Distinct().ToListAsync(ct);
        if (games.Count > 0)
        {
            var others = await context.RecordedGameParticipants.AsNoTracking().Where(p => p.UserID != placeholderId && games.Contains(p.RecordedGameID))
                .Select(p => p.Gamertag).ToListAsync(ct);
            if (others.Any(OtherOfTheHandle))
                return true;
        }
        return false;
    }

    internal sealed record Account(int Id, long? XboxUserID, string? UserName);

    private static string Reason(string rawTag, string canonical) => $"{ReasonPrefix}{rawTag} -> {canonical}";

    private static MergeOptions Options(string rawTag, string canonical, bool transferXbox)
    {
        var options = MergeOptions.For(MergeTrigger.Import, Reason(rawTag, canonical));
        options.TransferXbox = transferXbox;
        return options;
    }
}
