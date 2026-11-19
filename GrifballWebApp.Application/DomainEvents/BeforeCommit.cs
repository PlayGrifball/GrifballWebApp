using System.Collections.Concurrent;
using System.Reflection;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.DomainEvents;

/// <summary>
/// Reacts to a domain event of <typeparamref name="TEvent"/>'s type inside the change's own transaction: after its writes,
/// just before it commits, with the change's own scope (its context), so what it reads is the change as saved and what
/// it writes (an outbox message: a Discord post) is committed with the change, or not at all. Its failure fails the
/// change. For what must happen with the change; what may follow it is an <see cref="IDomainEventHandler{TEvent}"/>.
/// </summary>
public interface IBeforeCommitHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <param name="domainEvent">What happened.</param>
    /// <param name="commit">Every event of the commit, this one among them (a post may say what several did).</param>
    /// <param name="cancellationToken">The commit's.</param>
    Task Handle(TEvent domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken);
}

/// <summary>Runs the before-commit handlers of a commit's events (the Infrastructure's DomainEventsInterceptor calls it).</summary>
public interface IBeforeCommitDispatcher
{
    /// <summary>Whether any of <paramref name="domainEvents"/> has a before-commit handler (its change needs a transaction).</summary>
    bool Handles(IEnumerable<IDomainEvent> domainEvents);

    /// <summary>Runs <paramref name="domainEvents"/>' before-commit handlers from <paramref name="services"/>, the change's scope.</summary>
    Task Dispatch(IServiceProvider services, IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>Every event to the before-commit handlers of its own type, in the order the events were raised.</summary>
public sealed class BeforeCommitDispatcher(IServiceProviderIsService registered) : IBeforeCommitDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, IReadOnlyList<IDomainEvent>, CancellationToken, Task>> Invokers = new();

    private static readonly MethodInfo HandleOfType = typeof(BeforeCommitDispatcher).GetMethod(nameof(HandleAll), BindingFlags.NonPublic | BindingFlags.Static)!;

    public bool Handles(IEnumerable<IDomainEvent> domainEvents)
        => domainEvents.Any(e => registered.IsService(typeof(IBeforeCommitHandler<>).MakeGenericType(e.GetType())));

    public async Task Dispatch(IServiceProvider services, IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var invoke = Invokers.GetOrAdd(domainEvent.GetType(), type => HandleOfType.MakeGenericMethod(type)
                .CreateDelegate<Func<IServiceProvider, IDomainEvent, IReadOnlyList<IDomainEvent>, CancellationToken, Task>>());
            await invoke(services, domainEvent, domainEvents, cancellationToken);
        }
    }

    private static async Task HandleAll<TEvent>(IServiceProvider services, IDomainEvent domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IBeforeCommitHandler<TEvent>>())
            await handler.Handle((TEvent)domainEvent, commit, cancellationToken);
    }
}
