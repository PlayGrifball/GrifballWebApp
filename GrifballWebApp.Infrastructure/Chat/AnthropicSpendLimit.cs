using System.Net;
using System.Runtime.CompilerServices;
using Anthropic.Exceptions;
using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrifballWebApp.Infrastructure.Chat;

/// <summary>
/// A Claude model's client that says Anthropic's spend limits as the chat's refusal (<see cref="GrifChat.ProviderLimitReached"/>), not
/// as a failure, and logs each once, with Anthropic's request and workspace IDs: the limit on the key's workspace (or the
/// organization) someone set in Anthropic's console, the usage tier's own monthly cap, and the organization's credit run out. Any other
/// error stays as it is. As Anthropic's docs describe them, the tier's cap has the error code <see cref="TierCapCode"/> (on the HTTP
/// error, or a stream's error event); a limit someone set, and credit run out, are a 400 <c>invalid_request_error</c> with no code,
/// told apart only by how its message starts (<see cref="LimitMessages"/>), the one signal the docs give for them.
/// </summary>
public sealed class AnthropicSpendLimit(IChatClient inner, ILogger<AnthropicSpendLimit>? log = null) : DelegatingChatClient(inner)
{
    /// <summary>The error code of the usage tier's monthly cap.</summary>
    public const string TierCapCode = "enforced_spend_limit_reached";

    /// <summary>How the messages of a spend limit someone set, and of credit run out, begin.</summary>
    public static readonly IReadOnlyList<string> LimitMessages = ["You have reached your specified", "Your credit balance is too low"];

    private readonly ILogger _log = log ?? NullLogger<AnthropicSpendLimit>.Instance;

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (AnthropicServiceException e) when (IsSpendLimit(e))
        {
            throw Refused(e);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await NextAsync(updates))
            yield return updates.Current;
    }

    private async Task<bool> NextAsync(IAsyncEnumerator<ChatResponseUpdate> updates)
    {
        try
        {
            return await updates.MoveNextAsync();
        }
        catch (AnthropicServiceException e) when (IsSpendLimit(e))
        {
            throw Refused(e);
        }
    }

    /// <summary>The chat's refusal for <paramref name="limit"/>, which it keeps; the limit logged.</summary>
    private ChatRefusedException Refused(AnthropicServiceException limit)
    {
        var api = limit as AnthropicApiException;
        AnthropicLog.ProviderLimitReached(_log, limit.ErrorCode ?? limit.ApiMessage, api?.RequestID, api?.WorkspaceID);
        return new ChatRefusedException(GrifChat.ProviderLimitReached, limit);
    }

    /// <summary>Whether <paramref name="error"/> is one of Anthropic's spend limits.</summary>
    public static bool IsSpendLimit(AnthropicServiceException error)
        => error.ErrorCode == TierCapCode
            || (error is AnthropicApiException { StatusCode: HttpStatusCode.BadRequest } && error.ApiMessage is { } message
                && LimitMessages.Any(start => message.StartsWith(start, StringComparison.Ordinal)));
}

/// <summary>The chats' Infrastructure log lines.</summary>
internal static partial class AnthropicLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Anthropic refused for a spend limit ({Limit}; request {RequestId}, workspace {WorkspaceId}): Claude answers no one until it resets")]
    public static partial void ProviderLimitReached(ILogger logger, string? limit, string? requestId, string? workspaceId);
}
