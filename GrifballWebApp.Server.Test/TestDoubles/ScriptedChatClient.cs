using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A chat model that streams a reply it is given (each chunk after its delay) and records what it was asked: for
/// MessageCreateHandler's tests, in place of Ollama. <see cref="BeforeReply"/> runs first, with the request's options (its
/// tools), as a model calling a tool would.
/// </summary>
internal sealed class ScriptedChatClient(params (TimeSpan Delay, string Text)[] chunks) : IChatClient
{
    public List<ChatMessage> Messages { get; } = [];

    public ChatOptions? Options { get; private set; }

    public Func<ChatOptions, Task>? BeforeReply { get; init; }

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
                await Task.Delay(delay, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        // Nothing held
    }
}
