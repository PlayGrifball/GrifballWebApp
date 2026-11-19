using System.Data.Common;
using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrifballWebApp.Infrastructure.Services;

/// <summary>
/// The domain events the saved aggregates raised (<see cref="IHasDomainEvents"/>), to their handlers in two phases:
/// <list type="bullet">
/// <item>
/// Before the commit (<see cref="IBeforeCommitHandler{TEvent}"/>, through <see cref="IBeforeCommitDispatcher"/>): inside the
/// change's transaction, after all its saves and just before it commits, in the context's own scope, and what they add
/// (outbox messages) saved in it too. A save outside a transaction whose events have such handlers gets one of its own,
/// committed when the save is done. They fail the change when they fail.
/// </item>
/// <item>
/// After it (<see cref="IDomainEventDispatcher"/>): once the save or its transaction is committed, nothing for a rolled
/// back one or a failed save.
/// </item>
/// </list>
/// An aggregate's events are forgotten once its save succeeds, so a save that fails or is canceled keeps them for the
/// next try. The events saved wait for the commit of the transaction they were saved in, and that one only: a transaction
/// disposed without a rollback, or whose commit failed, never sends them, with a later save or transaction. A
/// before-commit handler that fails fails the commit after EF has accepted the save: the database rolls back, but the
/// tracked entities keep the values saved, so the scope (a request, a job) should end rather than save again. Every
/// entity the context tracks is read, deleted ones too (an aggregate's method raises the event of its removal before the
/// service removes it). One per context; without a scope (<paramref name="services"/>: a factory's context), no
/// before-commit handler can run, and a save that needs one is refused.
/// </summary>
public sealed class DomainEventsInterceptor(IDomainEventDispatcher dispatcher, IBeforeCommitDispatcher? beforeCommit = null,
    IServiceProvider? services = null) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    /// <summary>The events of the save under way: kept by the aggregates until it succeeds.</summary>
    private List<IHasDomainEvents> _saving = [];

    /// <summary>The events of this save and of earlier ones of a transaction not yet committed, for after the commit.</summary>
    private readonly List<IDomainEvent> _pending = [];

    /// <summary>The same, not yet handed to the before-commit handlers.</summary>
    private readonly List<IDomainEvent> _beforeCommit = [];

    /// <summary>The transaction <see cref="_pending"/>'s events were saved in: null for none.</summary>
    private DbTransaction? _pendingIn;

    /// <summary>The transaction this interceptor began for a save outside one, committed once the save is done.</summary>
    private bool _ownTransaction;

    /// <summary>The before-commit handlers are running: their own save is part of the commit, not one of its own.</summary>
    private bool _handling;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Note(eventData.Context!);
        if (NeedsTransaction(eventData.Context!))
            throw new InvalidOperationException("These changes' events have handlers that run in their transaction: save them asynchronously");
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context!;
        Note(context);
        if (NeedsTransaction(context))
        {
            await context.Database.BeginTransactionAsync(cancellationToken);
            _ownTransaction = true;
        }
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Saved(eventData.Context!);
        if (eventData.Context!.Database.CurrentTransaction is null)
            DispatchInBackground();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Saved(eventData.Context!);
        if (_handling)
            return result;
        var database = eventData.Context!.Database;
        if (_ownTransaction)
        {
            _ownTransaction = false;
            // The before-commit handlers run as it commits (TransactionCommittingAsync), the rest once it has
            try
            {
                await database.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await database.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }
        else if (database.CurrentTransaction is null)
        {
            await DispatchAsync();
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Unsaved(eventData.Context!);

    public override void SaveChangesCanceled(DbContextEventData eventData) => Unsaved(eventData.Context!);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        => UnsavedAsync(eventData.Context!);

    // EF calls this, not SaveChangesFailedAsync, for a canceled save: its own transaction mustn't be left open
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        => UnsavedAsync(eventData.Context!);

    /// <summary>The save failed or was canceled: the aggregates keep its events for the next try; a transaction's earlier saves wait for its end.</summary>
    private void Unsaved(DbContext context)
    {
        _saving = [];
        if (context.Database.CurrentTransaction is null)
            Forget();
    }

    private async Task UnsavedAsync(DbContext context)
    {
        if (_ownTransaction)
        {
            _ownTransaction = false;
            await context.Database.RollbackTransactionAsync(CancellationToken.None);
        }
        Unsaved(context);
    }

    private void Note(DbContext context)
        => _saving = context.ChangeTracker.Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

    /// <summary>A save outside a transaction whose events have before-commit handlers: it needs one (refused without a scope).</summary>
    private bool NeedsTransaction(DbContext context)
    {
        if (_handling || context.Database.CurrentTransaction is not null || beforeCommit is null
            || !beforeCommit.Handles(_saving.SelectMany(a => a.DomainEvents)))
            return false;
        if (services is null)
            throw new InvalidOperationException("These changes' events have handlers that run in their transaction, which a context made without a scope can't run");
        return true;
    }

    /// <summary>
    /// The save succeeded: its events wait for the commit of its transaction, and the aggregates are rid of them. Events
    /// still waiting from another transaction (disposed without a rollback) are dropped.
    /// </summary>
    private void Saved(DbContext context)
    {
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        if (!ReferenceEquals(transaction, _pendingIn))
        {
            Forget();
            _pendingIn = transaction;
        }
        foreach (var aggregate in _saving)
        {
            _pending.AddRange(aggregate.DomainEvents);
            _beforeCommit.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }
        _saving = [];
    }

    private void Forget()
    {
        _pending.Clear();
        _beforeCommit.Clear();
        _pendingIn = null;
    }

    /// <summary>A transaction commits: events waiting for another one (disposed without a rollback) are dropped.</summary>
    private void Only(DbTransaction transaction)
    {
        if (!ReferenceEquals(transaction, _pendingIn))
            Forget();
    }

    /// <summary>
    /// The before-commit handlers of the events saved so far, and their own changes saved in the transaction; again for any
    /// events those raise. Nothing without a dispatcher or a scope.
    /// </summary>
    private async Task BeforeCommitAsync(DbContext context, CancellationToken cancellationToken)
    {
        while (!_handling && _beforeCommit.Count > 0 && beforeCommit is not null && services is not null)
        {
            var events = _beforeCommit.ToList();
            _beforeCommit.Clear();
            _handling = true;
            try
            {
                await beforeCommit.Dispatch(services, events, cancellationToken);
                if (context.ChangeTracker.HasChanges())
                    await context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _handling = false;
            }
        }
    }

    private Task DispatchAsync()
    {
        _beforeCommit.Clear();
        if (_pending.Count == 0)
            return Task.CompletedTask;
        var events = _pending.ToList();
        _pending.Clear();
        // The change is made: whoever made it isn't stopped by a handler, nor by their request ending now
        return dispatcher.Dispatch(events, CancellationToken.None);
    }

    /// <summary>
    /// A save made synchronously: its events are sent without waiting for their handlers, rather than blocking on them. The
    /// dispatcher logs a handler's failure itself, and the save is made either way.
    /// </summary>
    private void DispatchInBackground() => _ = DispatchAsync();

    public InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        Only(transaction);
        if (!_handling && beforeCommit is not null && beforeCommit.Handles(_beforeCommit))
            throw new InvalidOperationException("These changes' events have handlers that run before their commit: commit asynchronously");
        return result;
    }

    public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Only(transaction);
        await BeforeCommitAsync(eventData.Context!, cancellationToken);
        return result;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        Only(transaction);
        DispatchInBackground();
    }

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Only(transaction);
        return DispatchAsync();
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Forget();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget();
        return Task.CompletedTask;
    }

    // A commit (or another use of the transaction) that failed: its events never go out
    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Forget();

    public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget();
        return Task.CompletedTask;
    }
}

/// <summary>No handlers to send the events to: a context made without the app's services (a test's, a design-time one).</summary>
public sealed class NoDomainEventHandlers : IDomainEventDispatcher
{
    public Task Dispatch(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
