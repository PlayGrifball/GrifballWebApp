using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The domain events' interceptor as the app wires it for a context: the Infrastructure's before-commit handlers (the
/// draft's and Late League's Discord posts, queued on the context's outbox in the change's transaction) with the context
/// it is added to, and no live updates. A test makes its context with it:
/// <c>GrifballContext context = null!; context = ...(AppDomainEvents.Interceptor(() => context));</c>
/// </summary>
internal static class AppDomainEvents
{
    public static DomainEventsInterceptor Interceptor(Func<GrifballContext> context)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => context());
        services.AddScoped<IApplicationDbContext>(_ => context());
        services.AddScoped<IOutbox>(_ => new ContextOutbox(context()));
        services.AddScoped<IDatabaseLocks>(_ => new ContextDatabaseLocks(context()));
        services.AddScoped<TeamService>();
        services.AddSingleton(TimeProvider.System);
        services.AddDomainEvents(typeof(InfrastructureServiceCollectionExtensions).Assembly);
        var provider = services.BuildServiceProvider();
        return new DomainEventsInterceptor(new NoDomainEventHandlers(), provider.GetRequiredService<IBeforeCommitDispatcher>(), provider);
    }
}

/// <summary>The domain events a context's commits sent, as its handlers would get them.</summary>
internal sealed class RecordedDomainEvents : IDomainEventDispatcher
{
    public List<GrifballWebApp.Domain.Shared.IDomainEvent> Sent { get; } = [];

    public Task Dispatch(IReadOnlyList<GrifballWebApp.Domain.Shared.IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        lock (Sent)
            Sent.AddRange(domainEvents);
        return Task.CompletedTask;
    }

    /// <summary>A context's interceptor that records what its commits send (no before-commit handlers).</summary>
    public DomainEventsInterceptor Interceptor() => new(this);
}
