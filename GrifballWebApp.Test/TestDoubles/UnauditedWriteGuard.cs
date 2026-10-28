using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Test;

/// <summary>
/// Fails a save that would write an <see cref="IAuditable"/> entity without its audit fields (no AuditInterceptor ran
/// before this one). <see cref="GrifballContextTestExtensions.FactoryFor"/> puts it on every context its test
/// IDbContextFactory hands out, so a handler that starts writing through the (unaudited) factory fails its tests.
/// </summary>
public class UnauditedWriteGuard : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Check(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            var unaudited = entry.State switch
            {
                EntityState.Added => entry.Entity.CreatedAt == default || entry.Entity.ModifiedAt == default,
                EntityState.Modified => !entry.Property(nameof(IAuditable.ModifiedAt)).IsModified,
                _ => false,
            };
            if (unaudited)
                throw new UnauditedWriteException($"{entry.Entity.GetType().Name} was {entry.State.ToString().ToLowerInvariant()} through an IDbContextFactory context without audit fields. Write through the scoped GrifballContext instead.");
        }
    }
}

public class UnauditedWriteException(string message) : Exception(message);
