using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrifballWebApp.Server.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// The outbox (<see cref="IOutbox"/>, scoped with its context) and its dispatcher. The dispatcher runs in preview
    /// without Discord too, skipping what is queued. <see cref="OutboxSignalInterceptor"/> still has to be added to the
    /// context's interceptors.
    /// </summary>
    public static IServiceCollection AddDiscordOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection("Outbox"));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<OutboxSignal>();
        services.AddScoped<OutboxSignalInterceptor>();
        services.AddScoped<IOutbox, ContextOutbox>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }

    public static IServiceCollection AddOutboxHandler<THandler>(this IServiceCollection services)
        where THandler : class, IOutboxHandler
    {
        services.AddScoped<IOutboxHandler, THandler>();
        return services;
    }
}
