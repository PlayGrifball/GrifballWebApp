using NetCord.Rest;

namespace GrifballWebApp.Server.Preview;

/// <summary>
/// Ephemeral PR preview environments (https://pr-N.grifball.xyz) run with <c>Preview__Enabled=true</c> and no Discord,
/// Halo Infinite or Google settings. In that mode the app never connects to Discord: no gateway, slash commands,
/// component interactions or Discord sign-in, and the queue/events background services don't run.
/// When the key is false or unset nothing here is used and the app behaves exactly as before.
/// </summary>
public static class PreviewMode
{
    public const string ConfigurationKey = "Preview:Enabled";

    public const string StartupMessage = "Preview mode is ON (Preview:Enabled=true): Discord gateway, commands, sign-in and the queue/events background services are disabled, and Discord REST calls fail locally";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(ConfigurationKey);

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
