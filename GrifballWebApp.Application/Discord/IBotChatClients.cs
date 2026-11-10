using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Discord;

/// <summary>The Discord bot's chat model: a client for one reply, its tools called for it (Infrastructure's OllamaChatClients).</summary>
public interface IBotChatClients
{
    IChatClient Create();
}
