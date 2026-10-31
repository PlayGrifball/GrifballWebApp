using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace GrifballWebApp.Server.Outbox;

/// <summary>Wakes <see cref="OutboxDispatcher"/> before its next poll, when outbox messages were committed.</summary>
public sealed class OutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        // At most one wake-up waiting: the dispatcher takes everything due each time it wakes.
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    /// <summary>True when notified within <paramref name="timeout"/>.</summary>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}

/// <summary>
/// Notifies <see cref="OutboxSignal"/> once a save that added outbox messages is committed: at once outside a
/// transaction, when the transaction commits inside one. A rolled back transaction's messages are gone, and nothing is
/// signaled. Scoped, with its context.
/// </summary>
public sealed class OutboxSignalInterceptor(OutboxSignal signal) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private bool _pending;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        NoteAdded(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        NoteAdded(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SignalIfCommitted(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        SignalIfCommitted(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void NoteAdded(DbContext? context)
    {
        if (context is not null && context.ChangeTracker.Entries<OutboxMessage>().Any(x => x.State == EntityState.Added))
            _pending = true;
    }

    private void SignalIfCommitted(DbContext? context)
    {
        if (_pending && context?.Database.CurrentTransaction is null)
            Signal();
    }

    private void Signal()
    {
        _pending = false;
        signal.Notify();
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (_pending)
            Signal();
    }

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => _pending = false;

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }
}
