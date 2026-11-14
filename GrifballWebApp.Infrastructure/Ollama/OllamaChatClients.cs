using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Ollama;

/// <summary>
/// The chats' model on Ollama: the server at Ollama:Url, the model Ollama:Model (else <see cref="DefaultModel"/>). No Ollama:Url,
/// no model: the chat is unavailable (the chart sets it only with a bundled or an external server; development's appsettings
/// point at Ollama's own default port). One client for the process, over the <see cref="HttpClientName"/> client, which never
/// times out itself: the host's <see cref="ChatHost.Timeout"/> decides, as a cold model on the CPU can take minutes to start
/// answering. Only the model: GrifChat calls the tools for it, so another provider is another <see cref="IChatClientFactory"/>
/// registered in its place.
/// </summary>
public sealed class OllamaChatClients : IChatClientFactory
{
    public const string DefaultModel = "llama3.1:latest";

    /// <summary>The HTTP client Ollama is called through (<see cref="OllamaChatExtensions.AddOllamaChat"/>).</summary>
    public const string HttpClientName = "Ollama";

    private readonly Lazy<OllamaApiClient>? _client;

    public OllamaChatClients(IConfiguration configuration, IHttpClientFactory httpClients)
    {
        Model = configuration["Ollama:Model"] is { Length: > 0 } model ? model : DefaultModel;
        if (configuration["Ollama:Url"] is not { Length: > 0 } url)
            return;
        var baseAddress = new Uri(url);
        _client = new(() =>
        {
            var http = httpClients.CreateClient(HttpClientName);
            http.BaseAddress = baseAddress;
            return new OllamaApiClient(http, Model);
        });
    }

    /// <summary>The model the chats use: Ollama:Model, else <see cref="DefaultModel"/>.</summary>
    public string Model { get; }

    public bool Available => _client is not null;

    public IChatClient Create()
        => (_client ?? throw new InvalidOperationException("Ollama:Url is not set: the chat has no model")).Value;
}

public static class OllamaChatExtensions
{
    /// <summary>
    /// The chats' model on Ollama (<see cref="OllamaChatClients"/>), and its HTTP client: no timeout of its own (the hosts' decide),
    /// its connections renewed every few minutes so a moved server is found again.
    /// </summary>
    public static IServiceCollection AddOllamaChat(this IServiceCollection services)
    {
        services.AddHttpClient(OllamaChatClients.HttpClientName, client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
        services.AddSingleton<IChatClientFactory, OllamaChatClients>();
        return services;
    }
}
