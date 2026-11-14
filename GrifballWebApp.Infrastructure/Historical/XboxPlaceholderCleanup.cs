using System.Reflection;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Infrastructure.Historical;

/// <summary>A placeholder the cleanup removed: its user ID and its Xbox account's gamertag.</summary>
public sealed record RemovedPlaceholder(int UserID, string Gamertag);

/// <summary>
/// Removes the Xbox placeholders made before only league players got one (<see cref="GamertagMatching.EnsureXboxPlaceholdersAsync"/>):
/// a placeholder user (IsDummyUser) holding an Xbox account that played in no league game, and that nothing in the database
/// names. Every foreign key to a user is checked (stats, teams, signups, claims, merges, championships, roles...), so a
/// placeholder that anything points at stays. The Xbox account and its games stay: only the user goes, and its Xbox page
/// shows the account without one. Run by <see cref="XboxPlaceholderCleanupJob"/>, from the Jobs page only.
/// </summary>
public sealed class XboxPlaceholderCleanup
{
    private static readonly MethodInfo ReferencedByKey = typeof(XboxPlaceholderCleanup).GetMethod(nameof(ReferencedBy), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ReferencedByNullableKey = typeof(XboxPlaceholderCleanup).GetMethod(nameof(ReferencedByNullable), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly GrifballContext _context;

    public XboxPlaceholderCleanup(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>Removes every such placeholder. Returns the ones removed.</summary>
    public async Task<List<RemovedPlaceholder>> RemoveAsync(CancellationToken ct = default)
    {
        var candidates = await _context.Users.AsNoTracking()
            .Where(u => u.IsDummyUser && u.XboxUserID != null)
            // In no league game (GamertagMatching.PlayedInALeagueGame's rule, inline: EF translates it here)
            .Where(u => !_context.SeasonSeriesGames.Any(g => g.Match != null
                && g.Match.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == u.XboxUserID))))
            .Select(u => new RemovedPlaceholder(u.Id, u.XboxUser!.Gamertag))
            .ToListAsync(ct);
        if (candidates.Count == 0)
            return [];

        var ids = candidates.Select(c => c.UserID).ToList();
        var named = new HashSet<int>();
        foreach (var key in ForeignKeysToUsers())
        {
            var property = key.Properties[0];
            var method = (property.ClrType == typeof(int) ? ReferencedByKey : ReferencedByNullableKey).MakeGenericMethod(key.DeclaringEntityType.ClrType);
            named.UnionWith(await (Task<List<int>>)method.Invoke(null, [_context, property.Name, ids, ct])!);
        }

        var removable = candidates.Where(c => !named.Contains(c.UserID)).ToList();
        var removableIds = removable.Select(c => c.UserID).ToList();
        await _context.Users.Where(u => removableIds.Contains(u.Id)).ExecuteDeleteAsync(ct);
        return removable;
    }

    /// <summary>Every single-column foreign key to a user, on an entity with a set of its own.</summary>
    private IEnumerable<IForeignKey> ForeignKeysToUsers()
        => _context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && !e.HasSharedClrType)
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(k => k.PrincipalEntityType.ClrType == typeof(User) && k.Properties.Count == 1
                && (k.Properties[0].ClrType == typeof(int) || k.Properties[0].ClrType == typeof(int?)));

    /// <summary>Which of <paramref name="ids"/> a row of <typeparamref name="TEntity"/> names in its <paramref name="property"/>.</summary>
    private static Task<List<int>> ReferencedBy<TEntity>(GrifballContext context, string property, List<int> ids, CancellationToken ct) where TEntity : class
        => context.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => ids.Contains(EF.Property<int>(e, property)))
            .Select(e => EF.Property<int>(e, property))
            .Distinct()
            .ToListAsync(ct);

    private static Task<List<int>> ReferencedByNullable<TEntity>(GrifballContext context, string property, List<int> ids, CancellationToken ct) where TEntity : class
        => context.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => EF.Property<int?>(e, property) != null && ids.Contains(EF.Property<int?>(e, property)!.Value))
            .Select(e => EF.Property<int?>(e, property)!.Value)
            .Distinct()
            .ToListAsync(ct);
}
