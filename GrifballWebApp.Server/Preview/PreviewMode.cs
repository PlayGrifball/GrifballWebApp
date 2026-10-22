using NetCord.Rest;

namespace GrifballWebApp.Server.Preview;

/// <summary>
/// Ephemeral PR preview environments run with <c>Preview__Enabled=true</c> and no Halo Infinite or Google settings.
/// Without a Discord bot token as well, the app never connects to Discord: no gateway, slash commands, component
/// interactions or Discord sign-in, and the queue/events background services don't run. A preview that holds one of
/// the preview slots gets that slot's bot and test server (<c>Discord:Token</c>, its channels, its OAuth app) and runs
/// Discord exactly as in normal mode (<see cref="HasDiscord"/>).
/// When the key is false or unset nothing here is used and the app behaves exactly as before.
/// </summary>
public static class PreviewMode
{
    public const string ConfigurationKey = "Preview:Enabled";

    public const string DiscordTokenKey = "Discord:Token";

    public const string StartupMessage = "Preview mode is ON (Preview:Enabled=true): Discord gateway, commands, sign-in and the queue/events background services are disabled, and Discord REST calls fail locally";

    public const string DiscordStartupMessage = "Preview mode is ON with Discord (Preview:Enabled=true and Discord:Token set): Discord runs as in normal mode, against this preview's own bot and server; the Halo Infinite health check is off";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);

    /// <summary>Whether a bot token is configured, as a preview slot provides.</summary>
    public static bool HasDiscord(IConfiguration configuration) => !string.IsNullOrWhiteSpace(configuration[DiscordTokenKey]);

    /// <summary>Preview mode without a bot token: Discord is off and <see cref="AddPreviewDiscord"/> stands in for it.</summary>
    public static bool DisablesDiscord(IConfiguration configuration) => IsEnabled(configuration) && !HasDiscord(configuration);

    /// <summary>
    /// Registers what the rest of the app needs from Discord, without Discord: <see cref="DiscordOptions"/> bound but not
    /// validated and forced to <see cref="DiscordOptions.DisableGlobally"/>, and a <see cref="RestClient"/> (normally
    /// provided by the gateway) whose every request fails before leaving the process.
    /// </summary>
    public static IServiceCollection AddPreviewDiscord(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordOptions>()
            .Bind(configuration.GetSection("Discord"))
            .PostConfigure(options => options.DisableGlobally = true);

        services.AddSingleton(_ => new RestClient(new RestClientConfiguration
        {
            RequestHandler = new PreviewDiscordRequestHandler(),
        }));

        return services;
    }
}

/// <summary>
/// Thrown instead of sending any request to Discord while preview mode is on.
/// </summary>
public sealed class PreviewDiscordDisabledException(string message) : InvalidOperationException(message);

internal sealed class PreviewDiscordRequestHandler : IRestRequestHandler
{
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        => Task.FromException<HttpResponseMessage>(new PreviewDiscordDisabledException(
            $"Discord is disabled in preview mode; not sending {request.Method} {request.RequestUri?.AbsolutePath}"));

    public void AddDefaultHeader(string name, IEnumerable<string> values)
    {
    }

    public void Dispose()
    {
    }
}
