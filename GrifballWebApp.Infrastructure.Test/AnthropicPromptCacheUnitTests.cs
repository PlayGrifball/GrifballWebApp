using System.Runtime.CompilerServices;
using Anthropic.Models.Messages;
using GrifballWebApp.Infrastructure.Chat;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// Anthropic's prompt cache on every call to a Claude model (<see cref="AnthropicPromptCache"/>): the request asks for automatic
/// caching (a top-level cache_control, five minutes), for the model and the output limit the call would have had.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AnthropicPromptCacheUnitTests
{
    private static readonly ChatMessage[] Question = [new(ChatRole.User, "Hi")];

    [Test]
    public async Task EveryStreamedCall_AsksForTheCache_WithTheModelAndTheOutputLimit()
    {
        var inner = new Recording();
        var client = new AnthropicPromptCache(inner, "claude-haiku-5-5");
        var options = new ChatOptions { MaxOutputTokens = 16_000, Instructions = "Be brief." };

        await foreach (var _ in client.GetStreamingResponseAsync(Question, options))
        {
            // Read to the end
        }

        var sent = (MessageCreateParams)inner.Options!.RawRepresentationFactory!(null!)!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.CacheControl, Is.Not.Null, "Automatic caching: the API places the breakpoint");
            Assert.That(sent.CacheControl!.Ttl, Is.Null, "Its default five minutes");
            Assert.That(sent.Model.Raw(), Is.EqualTo("claude-haiku-5-5"));
            Assert.That(sent.MaxTokens, Is.EqualTo(16_000));
            Assert.That(sent.Messages, Is.Empty, "The conversation is the client's to add");
            Assert.That(inner.Options.Instructions, Is.EqualTo("Be brief."), "The rest of the options as they were");
            Assert.That(options.RawRepresentationFactory, Is.Null, "The caller's options untouched");
        }
    }

    [Test]
    public async Task AWholeResponse_AsksForTheCacheToo_WithTheDefaultOutputLimit_WhenNoneIsAsked()
    {
        var inner = new Recording();
        var client = new AnthropicPromptCache(inner, "claude-opus-5-5");

        await client.GetResponseAsync(Question);

        var sent = (MessageCreateParams)inner.Options!.RawRepresentationFactory!(null!)!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.CacheControl, Is.Not.Null);
            Assert.That(sent.MaxTokens, Is.EqualTo(ChatModels.ClaudeMaxOutputTokens));
        }
    }

    [Test]
    public async Task ACallersOwnRequest_IsLeftAsItIs()
    {
        var inner = new Recording();
        var client = new AnthropicPromptCache(inner, "claude-haiku-5-5");
        Func<IChatClient, object?> own = _ => null;

        await client.GetResponseAsync(Question, new ChatOptions { RawRepresentationFactory = own });

        Assert.That(inner.Options!.RawRepresentationFactory, Is.SameAs(own));
    }

    /// <summary>A model that answers "Hello" and keeps the options it was called with.</summary>
    private sealed class Recording : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Options = options;
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Hello");
        }

        public object? GetService(System.Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing held
        }
    }
}
