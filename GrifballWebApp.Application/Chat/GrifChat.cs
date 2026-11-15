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
    /// A reply that takes over two minutes is given up, as on the site: the one model mustn't be held.
    /// </summary>
    public static readonly ChatHost Discord = new(
        "You are a Discord bot, and all messages must stay under the 2000-character limit.",
        "The bot also has slash commands; /help lists them.",
        InPublic: true,
        Timeout: TimeSpan.FromMinutes(2));

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
/// <c>ChatHub</c>), on Microsoft.Extensions.AI: the provider is <see cref="IChatClientFactory"/>'s registration's alone. A host keeps
/// the conversation, <see cref="Start"/>s a reply and shows it; all the rest is here, so no host can skip it: the question's and
/// the history's limits, the asker's (<see cref="ChatLimits"/>), the instructions, the host's tools as the caller, called for the
/// model until it answers, and the time a reply may take.
/// </summary>
public sealed class GrifChat(IChatClientFactory clients, IServiceScopeFactory scopes, ChatLimits limits)
{
    /// <summary>How many of the conversation's earlier messages the model is sent with a new one.</summary>
    public const int HistoryLimit = 25;

    /// <summary>The longest question a host takes: a Discord message's limit.</summary>
    public const int MaxQuestionLength = 2000;

    /// <summary>The longest earlier message the model is sent again: a longer one is cut there.</summary>
    public const int MaxHistoryMessageLength = 4000;

    /// <summary>What a host says when no model is configured (<see cref="Available"/>), in place of a reply.</summary>
    public const string Unavailable = "The chat isn't available right now: it has no model to answer with.";

    /// <summary>What a host says when a reply took longer than its <see cref="ChatHost.Timeout"/>.</summary>
    public const string TookTooLong = "That took too long to answer: ask again, or ask something simpler.";

    /// <summary>What a host says for a conversation it can't read (a message missing).</summary>
    public const string Unreadable = "That conversation can't be read: start a new one.";

    /// <summary>What a host says for no question.</summary>
    public const string NoQuestion = "Ask a question.";

    /// <summary>What a host says for a question over <see cref="MaxQuestionLength"/>.</summary>
    public static readonly string TooLong = $"Ask in {MaxQuestionLength} characters or fewer.";

    /// <summary>What the chat says in place of an answer to a question it never asked the model.</summary>
    private static readonly string[] Refused = [Unavailable, Unreadable, NoQuestion, TooLong, ChatLimits.Busy, ChatLimits.TooMany, ChatLimits.Full];

    /// <summary>Whether <paramref name="text"/> is a refusal the chat gave in place of an answer, or of the rest of one.</summary>
    public static bool IsRefusal(string text) => text == TookTooLong || Refused.Contains(text);

    /// <summary>
    /// <paramref name="history"/> without what the chat said in place of answers (where a host keeps them: a Discord DM is the channel
    /// itself), nor the questions it refused: neither is any part of the conversation the model had.
    /// </summary>
    public static List<ChatMessage> WithoutRefusals(IEnumerable<ChatMessage> history)
    {
        var kept = new List<ChatMessage>();
        foreach (var message in history)
        {
            if (message.Role != ChatRole.Assistant || !IsRefusal(message.Text))
            {
                kept.Add(message);
                continue;
            }
            // A question refused never reached the model; one that took too long did, and its answer so far stays
            if (message.Text != TookTooLong && kept.Count > 0 && kept[^1].Role == ChatRole.User)
                kept.RemoveAt(kept.Count - 1);
        }
        return kept;
    }

    /// <summary>Whether there is a model to ask: none configured, and a host says <see cref="Unavailable"/> instead.</summary>
    public bool Available => Clients.Available;

    private IChatClientFactory Clients { get; } = clients;

    private IServiceScopeFactory Scopes { get; } = scopes;

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
    /// What the model is sent: the last <see cref="HistoryLimit"/> messages of <paramref name="history"/> (oldest first,
    /// <see cref="WithoutRefusals"/>), each cut to <see cref="MaxHistoryMessageLength"/>, then <paramref name="question"/>.
    /// </summary>
    public static List<ChatMessage> Conversation(IEnumerable<ChatMessage> history, string question)
        => WithoutRefusals(history)
            .TakeLast(HistoryLimit)
            .Select(m => m.Text.Length > MaxHistoryMessageLength ? new ChatMessage(m.Role, m.Text[..MaxHistoryMessageLength]) : m)
            .Append(new ChatMessage(ChatRole.User, question))
            .ToList();

    /// <summary>The conversation so far as the browser kept it (<see cref="Refusal(ChatRequestDto)"/> found it readable).</summary>
    public static List<ChatMessage> History(ChatRequestDto request)
        => request.History.Select(t => new ChatMessage(t.FromBot ? ChatRole.Assistant : ChatRole.User, t.Text)).ToList();

    /// <summary>Why a question can't be asked (none, or over <see cref="MaxQuestionLength"/> characters), or null when it can.</summary>
    public static string? Refusal(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return NoQuestion;
        if (question.Length > MaxQuestionLength)
            return TooLong;
        return null;
    }

    /// <summary>
    /// Why a question to the site's chat can't be asked: its question's <see cref="Refusal(string)"/>, or a history it can't read
    /// (none, or a message missing: JSON's null passes for a required property); null when it can.
    /// </summary>
    public static string? Refusal(ChatRequestDto? request)
    {
        if (request is null)
            return Unreadable;
        // The browser's JSON can hold nulls the types don't allow
        IEnumerable<ChatTurnDto?>? history = request.History;
        if (history is null || history.Any(t => t?.Text is null))
            return Unreadable;
        return Refusal(request.Question);
    }

    /// <summary>
    /// A reply to <paramref name="question"/>, after <paramref name="history"/>, on <paramref name="host"/>, for
    /// <paramref name="asker"/> (<see cref="ChatLimits.Asker"/>): its slot taken now, held until the reply is done or disposed, or the
    /// asker stops waiting (<paramref name="cancellationToken"/>: freed that moment, so they may ask again at once, however long the
    /// reply takes to wind down). A <see cref="ChatRefusedException"/>, saying why, when there is no model, the question can't be
    /// asked, or the asker's limits are reached.
    /// </summary>
    public ChatReply Start(IEnumerable<ChatMessage> history, string question, ToolCaller caller, ChatHost host, string asker,
        CancellationToken cancellationToken = default)
    {
        if (!Available)
            throw new ChatRefusedException(Unavailable);
        if (Refusal(question) is { } refused)
            throw new ChatRefusedException(refused);
        var (slot, limited) = limits.TryStart(asker);
        if (slot is null)
            throw new ChatRefusedException(limited!);
        return new ChatReply(Clients, Scopes, Conversation(history, question), caller with { InPublic = host.InPublic }, host, slot, cancellationToken);
    }
}

/// <summary>A reply <see cref="GrifChat.Start"/>ed: <see cref="StreamAsync"/> it, then dispose it to free the asker's slot.</summary>
public sealed class ChatReply : IDisposable
{
    private readonly IChatClientFactory _clients;
    private readonly IServiceScopeFactory _scopes;
    private readonly List<ChatMessage> _conversation;
    private readonly ToolCaller _caller;
    private readonly ChatHost _host;
    private readonly IDisposable _slot;
    private readonly CancellationTokenRegistration _freedWhenStopped;

    internal ChatReply(IChatClientFactory clients, IServiceScopeFactory scopes, List<ChatMessage> conversation, ToolCaller caller, ChatHost host, IDisposable slot,
        CancellationToken stopped)
    {
        _clients = clients;
        _scopes = scopes;
        _conversation = conversation;
        _caller = caller;
        _host = host;
        _slot = slot;
        _freedWhenStopped = stopped.Register(slot.Dispose);
    }

    /// <summary>What the model is sent: the history's last messages, cut to their limit, then the question.</summary>
    public IReadOnlyList<ChatMessage> Conversation => _conversation;

    /// <summary>
    /// The model's reply, as it streams in. It is given the host's tools the caller may use (<see cref="GrifTools.AIFunctions"/>:
    /// each call in a scope of its own, a refusal its result), and they are called for it until it answers. Past the host's
    /// <see cref="ChatHost.Timeout"/>, a <see cref="ChatRefusedException"/> (<see cref="GrifChat.TookTooLong"/>). Canceled by
    /// <paramref name="cancellationToken"/> (the asker stopped waiting), the slot is freed at once, not once the reply has wound down.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var freed = cancellationToken.Register(_slot.Dispose);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_host.Timeout);
            // The tool calls are ours, not the provider's: any model the factory gives gets them. The model is shared: not disposed.
            var client = new ChatClientBuilder(_clients.Create()).UseFunctionInvocation().Build();
            var options = new ChatOptions { Instructions = GrifChat.Instructions(_host), Tools = GrifTools.AIFunctions(_scopes, _caller, _host.Tools) };
            await using var updates = client.GetStreamingResponseAsync(_conversation, options, timeout.Token).GetAsyncEnumerator(timeout.Token);
            while (await NextAsync(updates, timeout, cancellationToken))
                yield return updates.Current.Text;
            // A provider may end its stream quietly when canceled: past the timeout, that is no whole answer either
            if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                throw new ChatRefusedException(GrifChat.TookTooLong);
        }
        finally
        {
            // Done, failed or abandoned: the asker may ask again
            _slot.Dispose();
        }
    }

    /// <summary>The next update, if any; the host's timeout as <see cref="GrifChat.TookTooLong"/>.</summary>
    private static async Task<bool> NextAsync(IAsyncEnumerator<ChatResponseUpdate> updates, CancellationTokenSource timeout, CancellationToken asker)
    {
        try
        {
            return await updates.MoveNextAsync();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !asker.IsCancellationRequested)
        {
            throw new ChatRefusedException(GrifChat.TookTooLong);
        }
    }

    public void Dispose()
    {
        _freedWhenStopped.Dispose();
        _slot.Dispose();
    }
}

/// <summary>Why the chat gives no (whole) answer, for the host to show: no model, a question it can't take, a limit, the time.</summary>
public sealed class ChatRefusedException(string message) : Exception(message);
