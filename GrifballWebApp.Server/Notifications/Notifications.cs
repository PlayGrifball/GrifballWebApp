using System.Reflection;

namespace GrifballWebApp.Server.Notifications;

// In-process notifications: something happened (a captain added, captains locked, the queue changed), and every handler
// registered for its type reacts (SignalR clients, Discord messages, the queue display). This replaced MediatR, which
// needs a paid license from version 13 on; only its notifications were used, with the same names as here.

/// <summary>Something that happened, for every <see cref="INotificationHandler{TNotification}"/> of its type.</summary>
public interface INotification;

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

public interface IPublisher
{
    /// <summary>Runs every handler registered for <typeparamref name="TNotification"/>.</summary>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

/// <summary>
/// Runs a notification's handlers one after another, in registration order, as MediatR's default publisher did: the first
/// one to throw stops the rest, and its exception is the publish's.
/// </summary>
public sealed class Publisher(IServiceProvider services) : IPublisher
{
    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        foreach (var handler in services.GetServices<INotificationHandler<TNotification>>())
            await handler.Handle(notification, cancellationToken);
    }
}

public static class NotificationServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="IPublisher"/>, and every concrete notification handler in <paramref name="assembly"/> for each notification
    /// type it handles. All transient, as MediatR registered them: a publisher resolved in a request uses that request's
    /// scope for its handlers.
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services, Assembly assembly)
    {
        services.AddTransient<IPublisher, Publisher>();
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
        {
            foreach (var handler in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)))
                services.AddTransient(handler, type);
        }
        return services;
    }
}
