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

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The bot only streams");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Messages.AddRange(messages);
        Options = options;
        if (BeforeReply is not null)
            await BeforeReply(options!);
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
