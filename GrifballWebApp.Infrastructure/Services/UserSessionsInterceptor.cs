using System.Data.Common;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Infrastructure.Services;

/// <summary>
/// Ends the live sessions (<see cref="IUserSessions"/>) of the users whose sign-in or roles a save changed, once it is committed:
/// a user deleted, their security stamp changed (a sign-out, a new password), their lockout changed, a role added or removed.
/// Whatever made the change (Identity's managers, a merge, a sysadmin's edit), their open connections close and come back as
/// they are now. At once outside a transaction, when it commits inside one; nothing for a rolled back one. Scoped, with its context.
/// </summary>
public sealed class UserSessionsInterceptor(IUserSessions sessions) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private static readonly string[] SignInProperties = [nameof(User.SecurityStamp), nameof(User.LockoutEnd), nameof(User.LockoutEnabled)];

    /// <summary>The users changed by this save, and by earlier ones of a transaction not yet committed.</summary>
    private readonly HashSet<int> _pending = [];

    /// <summary>The users whose sign-in or roles <paramref name="context"/>'s changes touch.</summary>
    public static IEnumerable<int> Changed(DbContext context)
        => context.ChangeTracker.Entries<User>().Where(SignInChanged).Select(e => e.Entity.Id)
            .Concat(context.ChangeTracker.Entries<UserRole>().Where(e => e.State is EntityState.Added or EntityState.Deleted).Select(e => e.Entity.UserId));

    /// <summary>
    /// A user deleted, or one of their <see cref="SignInProperties"/> now other than it was: Identity's UserStore marks every property
    /// modified on any update (a failed sign-in's count included), so a property marked modified is no change to go by.
    /// </summary>
    private static bool SignInChanged(EntityEntry<User> entry)
        => entry.State == EntityState.Deleted
           || (entry.State == EntityState.Modified
               && SignInProperties.Select(entry.Property).Any(p => !Equals(p.OriginalValue, p.CurrentValue)));

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Note(eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Note(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context!.Database.CurrentTransaction is null)
            End();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(SavedChanges(eventData, result));

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        // Nothing of a failed save happened; a transaction's earlier saves are kept for its commit or rollback
        if (eventData.Context!.Database.CurrentTransaction is null)
            _pending.Clear();
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }

    private void Note(DbContext context) => _pending.UnionWith(Changed(context));

    /// <summary>Ends the pending users' sessions: this process's at once, the others' in the background (the save never waits on Redis).</summary>
    private void End()
    {
        if (_pending.Count == 0)
            return;
        var users = _pending.ToList();
        _pending.Clear();
        sessions.End(users);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => End();

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        End();
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => _pending.Clear();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }
}

/// <summary>No live sessions to end: the hosts without the site's hubs (the seeder, the converter, tests).</summary>
public sealed class NoUserSessions : IUserSessions
{
    public void End(IReadOnlyCollection<int> userIds)
    {
        // No connections in this process, and no other process to tell
    }
}
