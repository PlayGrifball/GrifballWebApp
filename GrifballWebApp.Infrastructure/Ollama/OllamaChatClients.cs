using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Ollama;

/// <summary>
/// The chats' model on Ollama (Ollama:Url, else Ollama's own default port): llama3.1. Only the model: GrifChat calls the tools
/// for it, so another provider is another <see cref="IChatClientFactory"/> registered in its place.
/// </summary>
public sealed class OllamaChatClients(IConfiguration configuration) : IChatClientFactory
{
    public const string Model = "llama3.1:latest";

    private Uri Url { get; } = new(configuration["Ollama:Url"] ?? "http://localhost:11434/");

    public IChatClient Create() => new OllamaApiClient(Url, Model);
}
