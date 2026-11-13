using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// Refuses each save <paramref name="refuses"/> says to (looking at the context's tracked changes), with a
/// DbUpdateException of <paramref name="message"/>, as the database refuses a row: a test's way to fail a service's save
/// at a chosen point, and check what it does then.
/// </summary>
internal sealed class RefusingSaves(Func<DbContext, bool> refuses, string message = RefusingSaves.DefaultMessage) : SaveChangesInterceptor
{
    public const string DefaultMessage = "The database refused it";

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
        => eventData.Context is { } context && refuses(context)
            ? throw new DbUpdateException(message)
            : base.SavingChangesAsync(eventData, result, cancellationToken);
}
