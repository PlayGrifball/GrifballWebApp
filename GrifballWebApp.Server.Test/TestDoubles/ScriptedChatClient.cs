using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using System.Runtime.CompilerServices;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A chat model that streams a reply it is given (each chunk once <see cref="Clock"/> has moved on by its delay, no real
/// wait) and records what it was asked: for
/// MessageCreateHandler's tests, in place of Ollama. <see cref="BeforeReply"/> runs first, with the request's options (its
/// tools), as a model calling a tool would.
/// </summary>
internal sealed class ScriptedChatClient(params (TimeSpan Delay, string Text)[] chunks) : IChatClient
{
    public List<ChatMessage> Messages { get; } = [];

    public ChatOptions? Options { get; private set; }

    public Func<ChatOptions, Task>? BeforeReply { get; init; }

    /// <summary>The clock a chunk's delay moves on: the one the handler reads.</summary>
    public FakeTimeProvider? Clock { get; init; }

    /// <summary>A tool call the model makes first, alone: the chat runs it and asks again, and the chunks are the reply then.</summary>
    public FunctionCallContent? Call { get; init; }

    private int _requests;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The bot only streams");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Messages.AddRange(messages);
        Options = options;
        if (BeforeReply is not null)
            await BeforeReply(options!);
        if (Call is not null && Interlocked.Increment(ref _requests) == 1)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [Call]);
            yield break;
        }
        foreach (var (delay, text) in chunks)
        {
            if (delay > TimeSpan.Zero)
                (Clock ?? throw new InvalidOperationException("A delayed chunk needs the Clock")).Advance(delay);
            yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        // Nothing held
    }
}

/// <summary>
/// One chat model that is <paramref name="models"/> in turn, one per reply: for a test of several messages, as the chats' model is one
/// client for the process (ChatModel wraps it once).
/// </summary>
internal sealed class ChatClientSequence(params IChatClient[] models) : IChatClient
{
    private int _next;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The bot only streams");

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => models[Interlocked.Increment(ref _next) - 1].GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        // The models are the test's
    }
}
