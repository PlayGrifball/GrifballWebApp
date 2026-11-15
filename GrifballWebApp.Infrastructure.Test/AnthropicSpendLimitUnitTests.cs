using System.Net;
using System.Runtime.CompilerServices;
using Anthropic.Exceptions;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Chat;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// Anthropic's spend limits as the chat says them (<see cref="AnthropicSpendLimit"/>): a workspace's or organization's limit someone set
/// (400, "You have reached your specified ... API usage limits") and the usage tier's cap (429, enforced_spend_limit_reached) refuse
/// with <see cref="GrifChat.ProviderLimitReached"/>; any other error stays as it is.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AnthropicSpendLimitUnitTests
{
    /// <summary>The body Anthropic sends with an error (its docs' shape).</summary>
    private static string Body(string type, string message, string? errorCode = null)
    {
        var details = errorCode is null ? "" : $$""", "details": { "error_code": "{{errorCode}}" }""";
        return $$"""{ "type": "error", "error": { "type": "{{type}}", "message": "{{message}}"{{details}} } }""";
    }

    private static readonly AnthropicApiException WorkspaceLimit = new AnthropicBadRequestException { StatusCode = HttpStatusCode.BadRequest, ResponseBody = Body("invalid_request_error",
        "You have reached your specified workspace API usage limits. You will regain access on 2026-12-01 at 00:00 UTC.") };

    private static readonly AnthropicApiException OrganizationLimit = new AnthropicBadRequestException { StatusCode = HttpStatusCode.BadRequest, ResponseBody = Body("invalid_request_error",
        "You have reached your specified API usage limits. You will regain access on 2026-12-01 at 00:00 UTC.") };

    private static readonly AnthropicApiException TierCap = new AnthropicRateLimitException { StatusCode = (HttpStatusCode)429, ResponseBody = Body("rate_limit_error",
        "You have reached your API usage limits: your organization has crossed its monthly API usage threshold.", "enforced_spend_limit_reached") };

    private static readonly AnthropicApiException RateLimit = new AnthropicRateLimitException { StatusCode = (HttpStatusCode)429, ResponseBody = Body("rate_limit_error",
        "Number of request tokens has exceeded your per-minute rate limit.", "rate_limit_exceeded") };

    private static readonly AnthropicApiException BadRequest = new AnthropicBadRequestException { StatusCode = HttpStatusCode.BadRequest, ResponseBody = Body("invalid_request_error",
        "messages: at least one message is required") };

    public static IEnumerable<TestCaseData> SpendLimits()
    {
        yield return new TestCaseData(WorkspaceLimit).SetName("A workspace's spend limit");
        yield return new TestCaseData(OrganizationLimit).SetName("The organization's spend limit");
        yield return new TestCaseData(TierCap).SetName("The usage tier's cap");
    }

    public static IEnumerable<TestCaseData> OtherErrors()
    {
        yield return new TestCaseData(RateLimit).SetName("A rate limit");
        yield return new TestCaseData(BadRequest).SetName("A bad request");
    }

    [TestCaseSource(nameof(SpendLimits))]
    public void AStream_StoppedByASpendLimit_IsRefusedWithWhy(AnthropicApiException error)
    {
        var client = new AnthropicSpendLimit(new Failing(error));

        Assert.That(async () => await Read(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")])),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.ProviderLimitReached));
    }

    [TestCaseSource(nameof(SpendLimits))]
    public void AResponse_StoppedByASpendLimit_IsRefusedWithWhy(AnthropicApiException error)
    {
        var client = new AnthropicSpendLimit(new Failing(error));

        Assert.That(async () => await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")]),
            Throws.TypeOf<ChatRefusedException>().With.Message.EqualTo(GrifChat.ProviderLimitReached));
    }

    [TestCaseSource(nameof(OtherErrors))]
    public void AnyOtherError_StaysAsItIs(AnthropicApiException error)
    {
        var client = new AnthropicSpendLimit(new Failing(error));

        Assert.That(async () => await Read(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")])), Throws.TypeOf(error.GetType()));
    }

    [Test]
    public async Task AStream_WithNoError_PassesThrough()
    {
        var client = new AnthropicSpendLimit(new Failing(null));

        var updates = await Read(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updates.Select(u => u.Text), Is.EqualTo(Answer));
            Assert.That(GrifChat.IsRefusal(GrifChat.ProviderLimitReached), Is.True, "Left out of the conversation the model is sent");
        }
    }

    private static readonly string[] Answer = ["Hello"];

    private static async Task<List<ChatResponseUpdate>> Read(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        var read = new List<ChatResponseUpdate>();
        await foreach (var update in updates)
            read.Add(update);
        return read;
    }

    /// <summary>A model that answers "Hello", then fails with <paramref name="error"/> (none: it just ends).</summary>
    private sealed class Failing(Exception? error) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => error is null ? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello"))) : Task.FromException<ChatResponse>(error);

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Hello");
            if (error is not null)
                throw error;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // Nothing held
        }
    }
}
