using System.Runtime.CompilerServices;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.AI;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// A host of the chat (the Discord bot in a channel or a DM, the site): what it tells the model about itself, whether others
/// read its replies, and how long one may take. Whether others read them is what sets the rest apart: in public (a Discord
/// channel) the caller sees seasons as the public does and gets <see cref="GrifTools.Chat"/>; in private (a DM, the site) their
/// roles decide what they see, and they get <see cref="GrifTools.PrivateChat"/>, staff's tools too, each for its roles.
/// </summary>
/// <param name="About">The first line of the model's instructions: what it is, and how it must write.</param>
/// <param name="More">The last line: what else the host offers.</param>
/// <param name="InPublic">Others read the replies (a Discord channel): <see cref="ToolCaller.InPublic"/>, whoever asks.</param>
/// <param name="Timeout">How long a reply may take, its tool calls included, before it is canceled.</param>
public sealed record ChatHost(string About, string More, bool InPublic, TimeSpan Timeout)
{
    /// <summary>
    /// The Discord bot in a channel: others read it there, so it sees seasons as the public does (#435), and gives no staff tools.
    /// Its replies are never cut short.
    /// </summary>
    public static readonly ChatHost Discord = new(
        "You are a Discord bot, and all messages must stay under the 2000-character limit.",
        "The bot also has slash commands; /help lists them.",
        InPublic: true,
        Timeout: System.Threading.Timeout.InfiniteTimeSpan);

    /// <summary>The Discord bot in a DM: the author's alone, so in private, as on the site.</summary>
    public static readonly ChatHost DiscordDirect = Discord with { InPublic = false };

    /// <summary>
    /// The site's chat: its replies are the signed-in user's alone, so in private. The page shows plain text, and a reply that takes
    /// over two minutes is given up.
    /// </summary>
    public static readonly ChatHost Site = new(
        "You are the chat on the Grifball league's website. Write plain text, not Markdown, and keep answers short.",
        "The site's pages show more than your tools do (a season's page, a player's profile, the leaderboards): point the person to them when it helps.",
        InPublic: false,
        Timeout: TimeSpan.FromMinutes(2));

    /// <summary>The names of the tools the model is given, each only to a caller its roles allow (<see cref="GrifTools.AIFunctions"/>).</summary>
    public IReadOnlyCollection<string> Tools => InPublic ? GrifTools.Chat : GrifTools.PrivateChat;
}

/// <summary>
/// The chat with the league's model, the same for every host (the Discord bot's <c>MessageCreateHandler</c>, the site's
/// <c>ChatHub</c>): its instructions, the conversation it is sent (the last <see cref="HistoryLimit"/> messages), the host's
/// tools (<see cref="ChatHost.Tools"/>) as the caller, called for the model until it answers, and the time a reply may
/// take. All of it on Microsoft.Extensions.AI: the provider is <see cref="IChatClientFactory"/>'s registration's alone. A host
/// keeps the conversation and shows the reply.
/// </summary>
public sealed class GrifChat(IChatClientFactory clients, IServiceScopeFactory scopes)
{
    /// <summary>How many of the conversation's earlier messages the model is sent with a new one.</summary>
    public const int HistoryLimit = 25;

    /// <summary>The longest question a host takes: a Discord message's limit.</summary>
    public const int MaxQuestionLength = 2000;

    /// <summary>The longest earlier message the model is sent again: a longer one is cut there.</summary>
    public const int MaxHistoryMessageLength = 4000;

    /// <summary>What every host tells the model, between its own first and last lines.</summary>
    private static readonly string[] Shared =
    [
        "Your primary purpose is to provide stats and information about Grifball, which your tools look up.",
        "Tools take IDs: search finds players, teams and seasons by name. A season tool with no season uses the running one (else the one that ended last; for signups, the next to start), and a player tool with no player uses the person you are talking to.",
        "Write times in UTC, as the tools give them, and say they are UTC.",
        "Use the pull tools (pull_season_match_games, pull_xbox_account_games) only when the person asks for games to be pulled: each starts a pull from Halo that others wait behind.",
    ];

    /// <summary>What a host in private adds: staff's tools that change data are for when they are asked for.</summary>
    public const string PrivateLine =
        "A tool that changes the league's data (a result, a schedule, a bracket, a claim, a job) is only for when the person asks for that change: say what it did.";

    private static readonly string[] PrivateLines = [PrivateLine];

    /// <summary>The model's instructions on <paramref name="host"/>: in private, with <see cref="PrivateLine"/>.</summary>
    public static string Instructions(ChatHost host)
        => string.Join('\n', Shared.Prepend(host.About).Concat(host.InPublic ? Array.Empty<string>() : PrivateLines).Append(host.More));

    /// <summary>
    /// What the model is sent: the last <see cref="HistoryLimit"/> messages of <paramref name="history"/> (oldest first), each cut
    /// to <see cref="MaxHistoryMessageLength"/>, then <paramref name="question"/>.
    /// </summary>
    public static List<ChatMessage> Conversation(IEnumerable<ChatMessage> history, string question)
        => history
            .TakeLast(HistoryLimit)
            .Select(m => m.Text.Length > MaxHistoryMessageLength ? new ChatMessage(m.Role, m.Text[..MaxHistoryMessageLength]) : m)
            .Append(new ChatMessage(ChatRole.User, question))
            .ToList();

    /// <summary>What the model is sent for a question to the site's chat, its history as the browser kept it (<see cref="Conversation(IEnumerable{ChatMessage}, string)"/>).</summary>
    public static List<ChatMessage> Conversation(ChatRequestDto request)
        => Conversation(request.History.Select(t => new ChatMessage(t.FromBot ? ChatRole.Assistant : ChatRole.User, t.Text)), request.Question);

    /// <summary>Why a question can't be asked (none, or over <see cref="MaxQuestionLength"/> characters), or null when it can.</summary>
    public static string? Refusal(ChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return "Ask a question.";
        if (request.Question.Length > MaxQuestionLength)
            return $"Ask in {MaxQuestionLength} characters or fewer.";
        return null;
    }

    /// <summary>
    /// The model's reply to <paramref name="conversation"/> on <paramref name="host"/>, as it streams in. It is given the host's tools
    /// <paramref name="caller"/> may use (<see cref="GrifTools.AIFunctions"/>: each call in a scope of its own, a refusal its result),
    /// and they are called for it until it answers. Canceled after the host's <see cref="ChatHost.Timeout"/>.
    /// </summary>
    public async IAsyncEnumerable<string> ReplyAsync(IList<ChatMessage> conversation, ToolCaller caller, ChatHost host,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tools = GrifTools.AIFunctions(scopes, caller with { InPublic = host.InPublic }, host.Tools);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(host.Timeout);
        // The tool calls are ours, not the provider's: any model the factory gives gets them
        using var client = new ChatClientBuilder(clients.Create()).UseFunctionInvocation().Build();
        var options = new ChatOptions { Instructions = Instructions(host), Tools = tools };
        await foreach (var update in client.GetStreamingResponseAsync(conversation, options, timeout.Token))
            yield return update.Text;
    }
}
