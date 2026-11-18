using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.DomainEvents;

/// <summary>
/// Reacts to a domain event of <typeparamref name="TEvent"/>'s type once its save is committed. A handler of
/// <see cref="IDomainEvent"/> itself gets every event. Runs in a scope of its own (the save's may be ending), and its
/// failure is logged, never the save's: the change is already made.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Sends committed domain events to their handlers (the Infrastructure's DomainEventsInterceptor calls it).</summary>
public interface IDomainEventDispatcher
{
    Task Dispatch(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>
/// Every event to the handlers of its own type, then to those of every event (<see cref="IDomainEvent"/>), in the order
/// the events were raised. One scope for a dispatch. A handler that throws is logged and the rest still run.
/// </summary>
public sealed class DomainEventDispatcher(IServiceScopeFactory scopes, ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, IEnumerable<Task>>> Invokers = new();

    private static readonly MethodInfo HandleOfType = typeof(DomainEventDispatcher).GetMethod(nameof(HandleAll), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task Dispatch(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        if (domainEvents.Count == 0)
            return;
        await using var scope = scopes.CreateAsyncScope();
        foreach (var domainEvent in domainEvents)
        {
            var invoke = Invokers.GetOrAdd(domainEvent.GetType(), type => HandleOfType.MakeGenericMethod(type)
                .CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, IEnumerable<Task>>>());
            foreach (var handled in invoke(scope.ServiceProvider, domainEvent, cancellationToken))
            {
                try
                {
                    await handled;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "A handler of the domain event {Event} failed", domainEvent.GetType().Name);
                }
            }
        }
    }

    /// <summary>Each handler's run, started one at a time as the dispatch awaits the one before.</summary>
    private static IEnumerable<Task> HandleAll<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            yield return Run(() => handler.Handle((TEvent)domainEvent, cancellationToken));
        if (typeof(TEvent) == typeof(IDomainEvent))
            yield break;
        foreach (var handler in services.GetServices<IDomainEventHandler<IDomainEvent>>())
            yield return Run(() => handler.Handle(domainEvent, cancellationToken));
    }

    /// <summary>A handler that throws before its first await fails its task, like one that throws after.</summary>
    private static Task Run(Func<Task> handle)
    {
        try
        {
            return handle();
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
}

public static class DomainEventServiceCollectionExtensions
{
    /// <summary>The dispatcher, and every concrete domain event handler in <paramref name="assembly"/> (scoped).</summary>
    public static IServiceCollection AddDomainEvents(this IServiceCollection services, Assembly assembly)
    {
        services.TryAddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
        {
            foreach (var handler in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
                services.AddScoped(handler, type);
        }
        return services;
    }
}
