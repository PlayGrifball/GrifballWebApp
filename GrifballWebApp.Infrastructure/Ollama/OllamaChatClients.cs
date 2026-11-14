using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Ollama;

/// <summary>
/// The chats' model on Ollama: the server at Ollama:Url, the model Ollama:Model (else <see cref="DefaultModel"/>). No Ollama:Url,
/// no model: the chat is unavailable (the chart sets it only with a bundled or an external server; development's appsettings
/// point at Ollama's own default port). Only the model: GrifChat calls the tools for it, so another provider is another
/// <see cref="IChatClientFactory"/> registered in its place.
/// </summary>
public sealed class OllamaChatClients(IConfiguration configuration) : IChatClientFactory
{
    public const string DefaultModel = "llama3.1:latest";

    private Uri? Url { get; } = configuration["Ollama:Url"] is { Length: > 0 } url ? new Uri(url) : null;

    /// <summary>The model the chats use: Ollama:Model, else <see cref="DefaultModel"/>.</summary>
    public string Model { get; } = configuration["Ollama:Model"] is { Length: > 0 } model ? model : DefaultModel;

    public bool Available => Url is not null;

    public IChatClient Create()
        => new OllamaApiClient(Url ?? throw new InvalidOperationException("Ollama:Url is not set: the chat has no model"), Model);
}
