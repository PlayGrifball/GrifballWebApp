using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Extensions;

public static class DbContextExtensions
{
    /// <summary>
    /// Drops whatever a failed step left in the change tracker (added, changed or removed but not saved), so the next
    /// step's SaveChanges doesn't try to write it again, and the domain events its aggregates raised, so the next save
    /// doesn't send them for a change that was never made. Unchanged entities stay tracked.
    /// </summary>
    public static void DiscardPendingChanges(this DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is IHasDomainEvents aggregate)
                aggregate.ClearDomainEvents();
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }
}
