using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// The chats' model (the Discord bot's, the site's): a Microsoft.Extensions.AI client for one reply. Which provider it is
/// (Infrastructure's OllamaChatClients) is its registration's choice alone; <see cref="GrifChat"/> calls the tools for it.
/// </summary>
public interface IChatClientFactory
{
    IChatClient Create();
}
