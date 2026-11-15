using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// The chats' model as every reply calls it: <see cref="IChatClientFactory"/>'s, inside Microsoft.Extensions.AI's OpenTelemetry (the
/// GenAI semantic conventions), on <see cref="SourceName"/>. Its spans: <c>chat {model}</c> per call to the model, with its token
/// counts; the function invocation a reply adds over it (<see cref="ChatReply"/>) finds the source here, so its
/// <c>orchestrate_tools</c> span and an <c>execute_tool {tool}</c> span per tool call are on it too. Its meter's:
/// <c>gen_ai.client.token.usage</c>, <c>gen_ai.client.operation.duration</c> and the stream's chunk times. One wrapper per model, not
/// one a reply: each makes an ActivitySource and a Meter of its own. What people wrote (questions, answers, tools' arguments and
/// results) is left out unless <see cref="SensitiveDataKey"/> says otherwise, whatever OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT says.
/// </summary>
/// <param name="loggers">For the telemetry's and the function invocation's logs (a tool's failure, with its exception).</param>
public sealed class ChatModel(IChatClientFactory clients, ILoggerFactory loggers, bool sensitiveData = false)
{
    /// <summary>The chats' ActivitySource and Meter.</summary>
    public const string SourceName = "GrifballWebApp.Chat";

    /// <summary>Whether the telemetry carries what people wrote: off unless set to true. Users' own text: for debugging only.</summary>
    public const string SensitiveDataKey = "Chat:Telemetry:SensitiveData";

    private readonly Lock _gate = new();
    private (IChatClient Model, IChatClient Traced)? _last;

    /// <summary>Whether a model is configured (<see cref="IChatClientFactory.Available"/>).</summary>
    public bool Available => clients.Available;

    /// <summary>Whether the telemetry carries what people wrote (<see cref="SensitiveDataKey"/>).</summary>
    public bool SensitiveData => sensitiveData;

    public ILoggerFactory Loggers => loggers;

    /// <summary>The factory's model, traced: wrapped once, and again only if the factory gives another.</summary>
    public IChatClient Create()
    {
        var model = clients.Create();
        lock (_gate)
        {
            if (_last is { } last && ReferenceEquals(last.Model, model))
                return last.Traced;
            var traced = model.AsBuilder()
                .UseOpenTelemetry(loggers, SourceName, client => client.EnableSensitiveData = sensitiveData)
                .Build();
            _last = (model, traced);
            return traced;
        }
    }
}
