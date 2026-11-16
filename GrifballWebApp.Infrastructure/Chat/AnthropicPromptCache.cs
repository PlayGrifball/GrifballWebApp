using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Infrastructure.Chat;

/// <summary>
/// A Claude model's client that asks for Anthropic's prompt cache on every call: automatic caching (a top-level cache_control), which
/// places the breakpoint at the request's last block and moves it on as the conversation grows. A reply's calls resend the same tools,
/// instructions and conversation for each tool round, and a follow-up question resends it all again: read from the cache, that input
/// costs a tenth of its price (written to it, a quarter more, once). Five minutes, Anthropic's default, refreshed by each read: a
/// reply's rounds are seconds apart and a conversation's questions minutes. The chat's spend counts the cache's reads and writes at
/// their own prices (ChatPrice). The request is made here as the Anthropic client takes it (RawRepresentationFactory), with the model
/// and the output limit the call would have had (the one it asks, else the client's; the SDK's own 1,024); a caller's own
/// request is left as it is.
/// </summary>
public sealed class AnthropicPromptCache(IChatClient inner, string model) : DelegatingChatClient(inner)
{
    /// <summary>The Anthropic client's own output limit, for a call that asks none (as it would have had without the cache).</summary>
    public const int SdkDefaultMaxTokens = 1024;

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetResponseAsync(messages, Cached(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(messages, Cached(options), cancellationToken);

    /// <summary><paramref name="options"/> (a copy: the caller's are left as they are), their request asking for the cache.</summary>
    private ChatOptions Cached(ChatOptions? options)
    {
        if (options?.RawRepresentationFactory is not null)
            return options;
        var cached = options?.Clone() ?? new ChatOptions();
        var maxTokens = cached.MaxOutputTokens ?? SdkDefaultMaxTokens;
        var modelId = cached.ModelId ?? model;
        cached.RawRepresentationFactory = _ => new MessageCreateParams
        {
            CacheControl = new CacheControlEphemeral(),
            Model = modelId,
            MaxTokens = maxTokens,
            Messages = [],
        };
        return cached;
    }
}
