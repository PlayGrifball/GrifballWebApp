using System.Data.Common;
using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrifballWebApp.Infrastructure.Services;

/// <summary>
/// Sends the domain events the saved aggregates raised (<see cref="IHasDomainEvents"/>) to their handlers
/// (<see cref="IDomainEventDispatcher"/>) once the save is committed: at once outside a transaction, when it commits inside
/// one, never for a rolled back one or a failed save. An aggregate's events are forgotten once its save succeeds, so a save
/// that fails keeps them for the next try. Every entity the context tracks is read, deleted ones too (an aggregate's method
/// raises the event of its removal before the service removes it). One per context.
/// </summary>
public sealed class DomainEventsInterceptor(IDomainEventDispatcher dispatcher) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    /// <summary>The events of the save under way: kept by the aggregates until it succeeds.</summary>
    private List<IHasDomainEvents> _saving = [];

    /// <summary>The events of this save and of earlier ones of a transaction not yet committed.</summary>
    private readonly List<IDomainEvent> _pending = [];

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
        Saved();
        if (eventData.Context!.Database.CurrentTransaction is null)
            DispatchAsync().GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Saved();
        if (eventData.Context!.Database.CurrentTransaction is null)
            await DispatchAsync();
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        // The aggregates keep this save's events for the next try; a transaction's earlier saves wait for its end
        _saving = [];
        if (eventData.Context!.Database.CurrentTransaction is null)
            _pending.Clear();
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }

    private void Note(DbContext context)
        => _saving = context.ChangeTracker.Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

    /// <summary>The save succeeded: its events wait for the commit, and the aggregates are rid of them.</summary>
    private void Saved()
    {
        foreach (var aggregate in _saving)
        {
            _pending.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }
        _saving = [];
    }

    private Task DispatchAsync()
    {
        if (_pending.Count == 0)
            return Task.CompletedTask;
        var events = _pending.ToList();
        _pending.Clear();
        // The change is made: whoever made it isn't stopped by a handler, nor by their request ending now
        return dispatcher.Dispatch(events, CancellationToken.None);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => DispatchAsync().GetAwaiter().GetResult();

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        => DispatchAsync();

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => _pending.Clear();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }
}

/// <summary>No handlers to send the events to: a context made without the app's services (a test's, a design-time one).</summary>
public sealed class NoDomainEventHandlers : IDomainEventDispatcher
{
    public Task Dispatch(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
