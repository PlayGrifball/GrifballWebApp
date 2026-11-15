using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// The chats' models (the Discord bot's, the site's) and a Microsoft.Extensions.AI client for each, built once a model by <see cref="ChatModel"/>
/// and shared by every reply (not disposed by them). Which providers there are (Infrastructure's ChatClients: Ollama, Anthropic) is its registration's choice alone;
/// <see cref="GrifChat"/> chooses the model and calls the tools for it.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>The configured models, in order (<see cref="ChatModelOption"/>): none, and the hosts say the chat is unavailable.</summary>
    IReadOnlyList<ChatModelOption> Models { get; }

    /// <summary>Whether any model is configured.</summary>
    bool Available { get; }

    /// <summary>A client for <paramref name="model"/>, one of <see cref="Models"/>: a new one each time (<see cref="ChatModel"/> keeps it).</summary>
    IChatClient Create(ChatModelOption model);
}
