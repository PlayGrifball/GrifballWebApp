using System.Net;
using System.Runtime.CompilerServices;
using Anthropic.Exceptions;
using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Infrastructure.Chat;

/// <summary>
/// A Claude model's client that says Anthropic's spend limits as the chat's refusal (<see cref="GrifChat.ProviderLimitReached"/>), not
/// as a failure: the limit on the key's workspace (or the organization) someone set in Anthropic's console, and the usage tier's own
/// monthly cap. Any other error stays as it is. As Anthropic's docs describe them: a limit someone set is a 400 whose message starts
/// "You have reached your specified" (API usage limits, or workspace API usage limits); the tier's cap is a 429 with the error code
/// <see cref="TierCapCode"/>.
/// </summary>
public sealed class AnthropicSpendLimit(IChatClient inner) : DelegatingChatClient(inner)
{
    /// <summary>The error code of the usage tier's monthly cap.</summary>
    public const string TierCapCode = "enforced_spend_limit_reached";

    /// <summary>How a spend limit someone set begins its message.</summary>
    public const string SetLimitMessage = "You have reached your specified";

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (AnthropicApiException e) when (IsSpendLimit(e))
        {
            throw new ChatRefusedException(GrifChat.ProviderLimitReached);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await NextAsync(updates))
            yield return updates.Current;
    }

    private static async Task<bool> NextAsync(IAsyncEnumerator<ChatResponseUpdate> updates)
    {
        try
        {
            return await updates.MoveNextAsync();
        }
        catch (AnthropicApiException e) when (IsSpendLimit(e))
        {
            throw new ChatRefusedException(GrifChat.ProviderLimitReached);
        }
    }

    /// <summary>Whether <paramref name="error"/> is one of Anthropic's spend limits.</summary>
    public static bool IsSpendLimit(AnthropicApiException error)
        => error.ErrorCode == TierCapCode
            || (error.StatusCode == HttpStatusCode.BadRequest && error.ApiMessage?.StartsWith(SetLimitMessage, StringComparison.Ordinal) == true);
}
