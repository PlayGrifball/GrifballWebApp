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
/// <item>it is a bare Xbox placeholder: a dummy user named <c>historical:…</c>, holding an Xbox account, with no Discord link,
/// email, password or region (an archive or merge link, a sign-in, a sysadmin's edit are kept);</item>
/// <item>its Xbox account played in no league game (<see cref="GamertagMatching.LeagueXuids"/>);</item>
/// <item>nothing in the database names it: no foreign key to it, and no key-less user column (a merge's, a dismissal's, an
/// audit column) holds its ID (<see cref="UserReferences"/>).</item>
/// </list>
/// Each is checked again for that placeholder right before it is deleted, in a transaction of its own, so a row written
/// meanwhile (a roster, a claim, a report that made its account a league player) keeps it; one the delete still can't make
/// (a key that refuses it) is left. The Xbox account and its games stay. Run by XboxPlaceholderCleanupJob, from the Jobs
/// page only.
/// </summary>
public sealed class XboxPlaceholderCleanup
{
    private readonly IApplicationDbContext _context;

    public XboxPlaceholderCleanup(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>The bare Xbox placeholders whose accounts played in no league game.</summary>
    private IQueryable<User> Candidates()
    {
        var league = GamertagMatching.LeagueXuids(_context);
        return _context.Users
            .Where(u => u.IsDummyUser && u.XboxUserID != null && EF.Functions.Like(u.UserName!, "historical:%"))
            .Where(u => u.DiscordUserID == null && u.Email == null && u.PasswordHash == null && u.RegionID == null)
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
        var candidates = await Candidates().AsNoTracking()
            .Select(u => new RemovedPlaceholder(u.Id, u.XboxUser!.Gamertag))
            .ToListAsync(ct);
        var named = await Named(candidates.Select(c => c.UserID).ToList(), ct);

        var removed = new List<RemovedPlaceholder>();
        foreach (var candidate in candidates.Where(c => !named.Contains(c.UserID)))
        {
            if (await RemoveOneAsync(candidate.UserID, ct))
                removed.Add(candidate);
        }
        return removed;
    }

    /// <summary>Checks the placeholder again and deletes it, in one transaction; false when it no longer qualifies or can't go.</summary>
    private async Task<bool> RemoveOneAsync(int userID, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            if ((await Named([userID], ct)).Count > 0)
                return false;
            var deleted = await Candidates().Where(u => u.Id == userID).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            return deleted > 0;
        }
        catch (Exception e) when (e is DbUpdateException or System.Data.Common.DbException)
        {
            // A key that refuses the delete: something names it after all
            await transaction.RollbackAsync(ct);
            return false;
        }
    }
}
