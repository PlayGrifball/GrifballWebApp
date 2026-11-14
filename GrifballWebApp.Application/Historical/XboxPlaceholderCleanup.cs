using System.Data;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Historical;

/// <summary>A placeholder the cleanup removed: its user ID and its Xbox account's gamertag.</summary>
public sealed record RemovedPlaceholder(int UserID, string Gamertag);

/// <summary>
/// Removes the Xbox placeholders made before only league players got one (<see cref="GamertagMatching.EnsureXboxPlaceholdersAsync"/>).
/// A placeholder goes only when all of these hold:
/// <list type="bullet">
/// <item>it is untouched since it was made (<see cref="User.IsUntouchedXboxPlaceholderExpression"/>: no Discord link, sign-in,
/// region, lockout, rename or edit);</item>
/// <item>its Xbox account played in no league game (<see cref="GamertagMatching.LeagueXuids"/>);</item>
/// <item>nothing in the database names it: no foreign key to it, and no key-less user column (a merge's, a dismissal's, an
/// audit column) holds its ID (<see cref="UserReferences"/>).</item>
/// </list>
/// They are checked and deleted a batch at a time, each batch in a serializable transaction, so nothing written meanwhile can
/// slip between the check and the delete: a writer waits, or the transaction fails. Any failure stops the run and is thrown
/// (the batches done stay done; running it again carries on). The Xbox account and its games stay. Run by
/// XboxPlaceholderCleanupJob, from the Jobs page only.
/// </summary>
public sealed class XboxPlaceholderCleanup
{
    /// <summary>The placeholders checked and deleted together, well under SQL Server's 2,100 parameters a statement.</summary>
    public const int BatchSize = 200;

    private readonly IApplicationDbContext _context;

    public XboxPlaceholderCleanup(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>The untouched Xbox placeholders whose accounts played in no league game.</summary>
    private IQueryable<User> Candidates()
    {
        var league = GamertagMatching.LeagueXuids(_context);
        return _context.Users
            .Where(User.IsUntouchedXboxPlaceholderExpression)
            .Where(u => !league.Contains(u.XboxUserID!.Value));
    }

    /// <summary>Which of <paramref name="ids"/> anything in the database names.</summary>
    private async Task<HashSet<int>> Named(List<int> ids, CancellationToken ct)
    {
        var named = new HashSet<int>();
        var columns = UserReferences.ForeignKeys(_context.Model).Select(k => k.Properties[0]).Concat(UserReferences.KeylessColumns(_context.Model));
        foreach (var column in columns)
            named.UnionWith(await UserReferences.NamedIn(_context, column, ids, ct));
        return named;
    }

    /// <summary>Removes every such placeholder. Returns the ones removed.</summary>
    public async Task<List<RemovedPlaceholder>> RemoveAsync(CancellationToken ct = default)
    {
        var removed = new List<RemovedPlaceholder>();
        var after = 0;
        while (true)
        {
            var batch = await Candidates().AsNoTracking()
                .Where(u => u.Id > after)
                .OrderBy(u => u.Id)
                .Take(BatchSize)
                .Select(u => new RemovedPlaceholder(u.Id, u.XboxUser!.Gamertag))
                .ToListAsync(ct);
            if (batch.Count == 0)
                return removed;
            after = batch[^1].UserID;
            removed.AddRange(await RemoveBatchAsync(batch, ct));
        }
    }

    /// <summary>Checks a batch and deletes the placeholders nothing names, in one serializable transaction.</summary>
    private async Task<List<RemovedPlaceholder>> RemoveBatchAsync(List<RemovedPlaceholder> batch, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var named = await Named(batch.Select(c => c.UserID).ToList(), ct);
        var removable = batch.Where(c => !named.Contains(c.UserID)).ToList();
        var ids = removable.Select(c => c.UserID).ToList();
        if (ids.Count > 0)
            // Checked again in the delete: still untouched and in no league game
            await Candidates().Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return removable;
    }
}
