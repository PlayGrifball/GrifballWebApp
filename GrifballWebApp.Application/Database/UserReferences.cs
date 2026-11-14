using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Application.Database;

/// <summary>
/// Where the database names a user, read from the model so a new table is covered with no change here: the merge
/// (UserMergeService) and the placeholder cleanup (XboxPlaceholderCleanup) both walk them.
/// </summary>
public static class UserReferences
{
    /// <summary>Every single-column foreign key to a user, on an entity with a key and a set of its own.</summary>
    public static IEnumerable<IForeignKey> ForeignKeys(IModel model)
        => model.FindEntityType(typeof(User))!.GetReferencingForeignKeys()
            .Where(fk => fk.Properties.Count == 1 && OwnSet(fk.DeclaringEntityType));

    /// <summary>
    /// The columns that hold a user's ID with no foreign key: a merge's and a dismissal's users (<c>…UserID</c>), and every
    /// row's audit columns (<c>CreatedByID</c>, <c>ModifiedByID</c>). A key's columns too: a dismissal's pair of users is its key.
    /// </summary>
    public static IEnumerable<IProperty> KeylessColumns(IModel model)
        => model.GetEntityTypes()
            .Where(OwnSet)
            .SelectMany(e => e.GetDeclaredProperties())
            .Where(p => (p.ClrType == typeof(int) || p.ClrType == typeof(int?)) && !p.IsForeignKey()
                && (p.Name.EndsWith("UserID", StringComparison.Ordinal) || p.Name is "CreatedByID" or "ModifiedByID"));

    /// <summary>Which of <paramref name="ids"/> the column names, in any row of its entity.</summary>
    public static Task<List<int>> NamedIn(IApplicationDbContext context, IProperty column, List<int> ids, CancellationToken ct)
        => (Task<List<int>>)(column.ClrType == typeof(int) ? NamedByKey : NamedByNullableKey)
            .MakeGenericMethod(column.DeclaringType.ClrType)
            .Invoke(null, [context, column.Name, ids, ct])!;

    private static bool OwnSet(ITypeBase type) => type is IEntityType e && !e.IsOwned() && e.FindPrimaryKey() is not null && !e.HasSharedClrType;

    private static readonly System.Reflection.MethodInfo NamedByKey = typeof(UserReferences).GetMethod(nameof(NamedBy), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    private static readonly System.Reflection.MethodInfo NamedByNullableKey = typeof(UserReferences).GetMethod(nameof(NamedByNullable), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static Task<List<int>> NamedBy<TEntity>(IApplicationDbContext context, string property, List<int> ids, CancellationToken ct) where TEntity : class
        => context.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => ids.Contains(EF.Property<int>(e, property)))
            .Select(e => EF.Property<int>(e, property))
            .Distinct()
            .ToListAsync(ct);

    private static Task<List<int>> NamedByNullable<TEntity>(IApplicationDbContext context, string property, List<int> ids, CancellationToken ct) where TEntity : class
        => context.Set<TEntity>().IgnoreQueryFilters()
            .Where(e => EF.Property<int?>(e, property) != null && ids.Contains(EF.Property<int?>(e, property)!.Value))
            .Select(e => EF.Property<int?>(e, property)!.Value)
            .Distinct()
            .ToListAsync(ct);
}
