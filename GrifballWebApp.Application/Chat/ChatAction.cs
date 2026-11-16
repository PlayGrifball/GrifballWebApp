using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// A change the model asked for, waiting for the person's yes or no (<see cref="GrifChat.ConfirmAsync"/>): kept on the server
/// (<see cref="ChatAccounts.SaveWaitingAsync"/>), so what runs is what they were shown, never what a browser or a tool's result
/// says. <paramref name="Description"/> is what they're shown; <paramref name="Model"/> answers once they have.
/// </summary>
public sealed record ChatAction(string Id, string Model, string Description, IReadOnlyList<ChatActionCall> Calls)
{
    /// <summary>The model's calls in <paramref name="requests"/> as one action, waiting for <paramref name="model"/>'s caller.</summary>
    public static ChatAction From(IReadOnlyList<ToolApprovalRequestContent> requests, string model)
    {
        var calls = requests.Select(r => (Request: r, Call: (FunctionCallContent)r.ToolCall)).ToList();
        return new ChatAction(Guid.NewGuid().ToString("N"), model,
            string.Join("; ", calls.Select(c => Describe(c.Call))),
            calls.Select(c => new ChatActionCall(c.Request.RequestId, c.Call.CallId, c.Call.Name, JsonSerializer.Serialize(c.Call.Arguments))).ToList());
    }

    /// <summary>The most of an argument's value the person is shown; past it, cut with an ellipsis.</summary>
    public const int ShownValueLength = 200;

    /// <summary>
    /// A call as the person reads it: the tool and its arguments, <c>forfeit_season_match(seasonMatchID: 12, side: "home")</c>. Each value
    /// as JSON (a string quoted, its quotes escaped), so a value can't pass for more arguments or another call.
    /// </summary>
    public static string Describe(FunctionCallContent call)
        => $"{call.Name}({string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>()).Select(a => $"{a.Key}: {Shown(a.Value)}"))})";

    // Only what JSON itself needs escaped: a quote reads as \", not \u0022
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Shown(object? value)
    {
        var json = JsonSerializer.Serialize(value, Readable);
        return json.Length > ShownValueLength ? json[..ShownValueLength] + "…" : json;
    }

    /// <summary>The person's answer, as the function invocation takes it: approved (or not) for each call.</summary>
    public ChatMessage Answer(bool approved) => new(ChatRole.User,
        Calls.Select(c => (AIContent)new ToolApprovalResponseContent(c.RequestId, approved, new FunctionCallContent(c.CallId, c.Name,
            JsonSerializer.Deserialize<Dictionary<string, object?>>(c.Arguments)))).ToList());
}

/// <summary>One of an action's calls: the approval request's ID, the model's call ID, the tool and its arguments (JSON).</summary>
public sealed record ChatActionCall(string RequestId, string CallId, string Name, string Arguments);
