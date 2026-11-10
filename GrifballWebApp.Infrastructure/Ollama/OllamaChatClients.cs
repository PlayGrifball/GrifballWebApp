using GrifballWebApp.Application.Discord;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;

namespace GrifballWebApp.Infrastructure.Ollama;

/// <summary>The bot's chat model on Ollama (Ollama:Url, else Ollama's own default port): llama3.1, calling the tools it is given.</summary>
public sealed class OllamaChatClients(IConfiguration configuration) : IBotChatClients
{
    public const string Model = "llama3.1:latest";

    private Uri Url { get; } = new(configuration["Ollama:Url"] ?? "http://localhost:11434/");

    public IChatClient Create()
        => new ChatClientBuilder(new OllamaApiClient(Url, Model))
            .UseFunctionInvocation()
            .Build();
}
