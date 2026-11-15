using System.Collections.Concurrent;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// The chats' models as every reply calls them: each of <see cref="IChatClientFactory"/>'s, with the tool calls (the function invocation) over
/// Microsoft.Extensions.AI's OpenTelemetry (the GenAI semantic conventions), on <see cref="SourceName"/>. Its spans: an
/// <c>orchestrate_tools</c> span per reply, under it a <c>chat {model}</c> span per call to the model, with its token counts, and an
/// <c>execute_tool {tool}</c> span per tool call. Its meter's: <c>gen_ai.client.token.usage</c>, <c>gen_ai.client.operation.duration</c>
/// and the stream's chunk times. Built once a model, the first time it answers, and never replaced: the telemetry makes an
/// ActivitySource and a Meter of its own. What people wrote (questions, answers, tools' arguments, results and failures' messages) is
/// left out unless <see cref="SensitiveDataKey"/> says otherwise, whatever OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT says.
/// </summary>
public sealed class ChatModel
{
    /// <summary>The chats' ActivitySource and Meter.</summary>
    public const string SourceName = "GrifballWebApp.Chat";

    /// <summary>Whether the telemetry carries what people wrote: off unless set to true. Users' own text: for debugging only.</summary>
    public const string SensitiveDataKey = "Chat:Telemetry:SensitiveData";

    /// <summary>
    /// The most calls to the model one reply makes (its tool rounds and its answer): each resends the conversation so far, so a paid
    /// model's reply that keeps calling tools mustn't run on (Microsoft.Extensions.AI's own default is 40).
    /// </summary>
    public const int MaxModelCalls = 10;

    private readonly IChatClientFactory _clients;
    private readonly ILoggerFactory _loggers;
    private readonly ConcurrentDictionary<string, Lazy<IChatClient>> _pipelines = new(StringComparer.Ordinal);

    /// <summary>The chats' model, traced as <paramref name="sensitiveData"/> says.</summary>
    /// <param name="clients">The chats' model, whichever provider's.</param>
    /// <param name="loggers">For the telemetry's and the function invocation's logs (a tool's failure), and each tool call's (<see cref="ToolLog"/>).</param>
    /// <param name="sensitiveData">Whether the telemetry carries what people wrote (<see cref="SensitiveDataKey"/>).</param>
    public ChatModel(IChatClientFactory clients, ILoggerFactory loggers, bool sensitiveData = false)
    {
        _clients = clients;
        _loggers = loggers;
        SensitiveData = sensitiveData;
        ToolLog = loggers.CreateLogger(typeof(GrifTools).FullName!);
    }

    /// <summary>Whether a model is configured (<see cref="IChatClientFactory.Available"/>).</summary>
    public bool Available => _clients.Available;

    /// <summary>The configured models, in order (<see cref="IChatClientFactory.Models"/>).</summary>
    public IReadOnlyList<ChatModelOption> Models => _clients.Models;

    /// <summary>Whether the telemetry carries what people wrote (<see cref="SensitiveDataKey"/>).</summary>
    public bool SensitiveData { get; }

    /// <summary>Where each tool call is logged (<see cref="GrifTools.AIFunctions"/>).</summary>
    internal ILogger ToolLog { get; }

    /// <summary><paramref name="model"/>, its tool calls and its telemetry: the same client for every reply it answers.</summary>
    public IChatClient Create(ChatModelOption model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return _pipelines.GetOrAdd(model.Id, _ => new(() => Build(model))).Value;
    }

    private IChatClient Build(ChatModelOption model) => _clients.Create(model).AsBuilder()
        .UseFunctionInvocation(_loggers, invocation => invocation.MaximumIterationsPerRequest = MaxModelCalls)
        .UseOpenTelemetry(_loggers, SourceName, client => client.EnableSensitiveData = SensitiveData)
        .Build();

    /// <summary>
    /// <see cref="SensitiveDataKey"/>'s value: true or false (any case), else off, with a warning: a telemetry switch mustn't stop the
    /// chats.
    /// </summary>
    public static bool SensitiveDataFrom(IConfiguration configuration, ILogger log)
    {
        var value = configuration[SensitiveDataKey];
        if (string.IsNullOrEmpty(value))
            return false;
        if (bool.TryParse(value, out var on))
            return on;
        ChatLog.NotABoolean(log, SensitiveDataKey);
        return false;
    }
}
