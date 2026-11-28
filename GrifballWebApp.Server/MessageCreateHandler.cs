using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server;

public class MessageCreateHandler(IDiscordRestClient discordClient, IConfiguration configuration, IServiceScopeFactory scopeFactory, IAmbientCurrentUser ambientCurrentUser, ILogger<MessageCreateHandler> logger,
    GrifChat chat, TimeProvider? time = null) : IMessageCreateGatewayHandler
{
    /// <summary>The Discord bot's spans: one per message it answers (<see cref="MessageSpanName"/>), the chat's under it.</summary>
    public const string ActivitySourceName = "GrifballWebApp.Discord";

    /// <summary>The span of a message the bot answers, tagged <c>discord.direct</c> for a DM.</summary>
    public const string MessageSpanName = "Discord message";

    private static readonly ActivitySource Tracing = new(ActivitySourceName);

    private ulong BotId { get; } = ulong.Parse(configuration["Discord:ClientId"] ?? "0");

    /// <summary>The clock the streamed reply is paced by (the system's, unless a test gives one).</summary>
    private TimeProvider Time { get; } = time ?? TimeProvider.System;

    public async ValueTask HandleAsync(Message arg)
    {
        if (arg.Author.Id == BotId)
            return;

        // A DM is to the bot alone: it answers every one, no mention needed; a reply, only one to the bot's own message
        var toTheBot = arg.GuildId is null || arg.MentionedUsers.Any(x => x.Id == BotId);
        if (!toTheBot && arg.MessageReference is null)
            return;

        // A trace per message the bot answers (a gateway event is no request to sit under): the lookup of what a reply replies to,
        // then its reply's spans (the chat's model and tool calls), the database's and Discord's calls in it
        using var span = Tracing.StartActivity(MessageSpanName);
        span?.SetTag("discord.direct", arg.GuildId is null);
        if (!toTheBot)
        {
            // Not recorded until the bot is known to answer, so a reply between others leaves no trace, nor does the lookup's call
            // (its child, made meanwhile: not recorded either). The span still covers the lookup's time.
            var recorded = span?.Recorded == true;
            Unrecord(span);
            try
            {
                var replyToMessage = await discordClient.GetMessageAsync(arg.MessageReference!.ChannelId, arg.MessageReference.MessageId);
                toTheBot = replyToMessage is not null && replyToMessage.Author.Id == BotId;
            }
            catch (Exception ex)
            {
                // A message deleted, or one the bot can't read: one failing message must not fault the gateway handler
                logger.LogError(ex, "Failed to look up the message Discord message {MessageId} replies to", arg.Id);
                Record(span, recorded);
                // Its type: its message may name what a user can't see
                span?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                return;
            }
            if (toTheBot)
                Record(span, recorded);
        }
        span?.SetTag("discord.answered", toTheBot);
        if (toTheBot)
            await HandleResponse(arg, span);
    }

    /// <summary><paramref name="span"/> not recorded (not exported when it ends), nor its children made meanwhile.</summary>
    private static void Unrecord(System.Diagnostics.Activity? span)
    {
        if (span is not null)
            span.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }

    /// <summary>Records <paramref name="span"/> again, as it was sampled: exported when it ends.</summary>
    private static void Record(System.Diagnostics.Activity? span, bool recorded)
    {
        if (span is not null && recorded)
            span.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
    }

    private ValueTask HandleResponse(Message message, System.Diagnostics.Activity? span)
        => Handled(() => HandleResponseCore(message),
            ex => logger.LogError(ex, "Failed to handle message {MessageId} from Discord user {DiscordUserId}", message.Id, message.Author.Id), span);

    /// <summary>The answer, its failure logged (never thrown) and set on <paramref name="span"/>, as is a reply cut short.</summary>
    private async ValueTask Handled(Func<ValueTask<string?>> answer, Action<Exception> logFailure, System.Diagnostics.Activity? span)
    {
        // A fresh ambient user slot per message, filled in with the author in HandleResponseCore, for contexts with no scope of
        // their own (IDbContextFactory) and other scopes (each tool call's, GrifTools.AIFunctions)
        ambientCurrentUser.Begin();
        string? error;
        try
        {
            error = await answer();
        }
        catch (Exception ex)
        {
            // One failing message must not fault the gateway handler for every message after it.
            logFailure(ex);
            // Its type: its message may quote a user
            error = ex.GetType().Name;
        }
        finally
        {
            ambientCurrentUser.End();
        }
        // A failure, or a reply cut short (said and handled, but no whole answer): so a slow reply is found by its status too
        if (error is not null)
            span?.SetStatus(ActivityStatusCode.Error, error);
    }

    /// <summary>The message answered: why the answer was cut short, if it was (<see cref="AnswerAsync"/>).</summary>
    private ValueTask<string?> HandleResponseCore(Message message)
        => HandleResponseCore(message, message.Author.Id, message.Author.Username, message.Content, message.GuildId is null, null);

    /// <summary>A click on the Confirm or Cancel button of a DM's waiting change: the answer is the same as typing yes or no.</summary>
    public sealed record ChatClick(string ActionId, bool Approved);

    /// <summary>
    /// The click of <paramref name="userId"/> on a DM reply's Confirm or Cancel button (<paramref name="message"/> is that reply): answered as their yes or
    /// no would be, the reply to the button's message. A failure is logged, never thrown: the gateway handler must go on.
    /// </summary>
    public async Task HandleClickAsync(NetCord.Rest.RestMessage message, ulong userId, string username, ChatClick click)
    {
        using var span = Tracing.StartActivity(MessageSpanName);
        span?.SetTag("discord.direct", true);
        span?.SetTag("discord.answered", true);
        await Handled(() => HandleResponseCore(message, userId, username, "", true, click),
            ex => logger.LogError(ex, "Failed to handle the click of Discord user {DiscordUserId} on message {MessageId}", userId, message.Id), span);
    }

    /// <summary>
    /// The answers of <paramref name="userId"/> to a form the chat made (<paramref name="message"/> shows them): the model reads them as their next
    /// turn, and the reply is to that message. Refused (a reply under way, the limits), the chat says why and <paramref name="refused"/> runs, so the
    /// form can be answered again. A failure is logged, never thrown, as a message's is.
    /// </summary>
    public async Task HandleFormAsync(NetCord.Rest.RestMessage message, ulong userId, string username, string answers, bool direct, Func<Task> refused)
    {
        using var span = Tracing.StartActivity(MessageSpanName);
        span?.SetTag("discord.direct", direct);
        span?.SetTag("discord.answered", true);
        await Handled(() => HandleResponseCore(message, userId, username, answers, direct, null, refused),
            ex => logger.LogError(ex, "Failed to handle the form answers of Discord user {DiscordUserId} in message {MessageId}", userId, message.Id), span);
    }

    /// <summary>Whether the model may make forms here (<see cref="ChatForms.EnabledKey"/>): on unless the setting turns them off.</summary>
    private bool FormsOn => configuration.GetValue(ChatForms.EnabledKey, true);

    /// <summary>Who a reply answers (<see cref="ChatLimits.Asker"/>), and whether in a DM: what a form it offers is held for.</summary>
    private sealed record Asking(string Asker, bool Direct);

    /// <summary>The form a reply offered, held for its asker until they answer it or it expires (<see cref="ChatForms.Lasts"/>): its ID and title, for the Fill in button.</summary>
    private async Task<(string ID, string Title)> HoldAsync(ChatForm form, Asking asking)
    {
        var id = ChatForms.NewId();
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChatForms>().SaveAsync(id, new HeldForm(form, asking.Asker, asking.Direct), ChatForms.Lasts);
        return (id, form.Title);
    }

    private async ValueTask<string?> HandleResponseCore(NetCord.Rest.RestMessage message, ulong authorId, string authorName, string question, bool direct, ChatClick? click,
        Func<Task>? refused = null)
    {
        // No model configured (no Ollama:Url): say so, rather than fail on each message
        if (!chat.Available)
        {
            await message.ReplyAsync(GrifChat.Unavailable);
            return null;
        }

        var UserId = (long)authorId;
        var BotID = (long)BotId;
        // The author's linked app user, if any, is the audit user for this scope's writes and the ambient user.
        using var scope = scopeFactory.CreateScope();
        await DiscordInteractionPipeline.SetCurrentUserAsync(authorId, logger, scope.ServiceProvider);
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        await EnsureDiscordUsersAsync(context, UserId, authorName, BotID);
        // A DM is the author's alone: in private, as the site's chat (their roles decide which seasons they see, and staff get their
        // tools), its history Discord's DM channel itself, and nothing of it kept in DiscordMessages, whose history a channel's reply
        // reads: what staff see in private never reaches a public reply. In a channel, in public: the channel reads the reply, so a
        // season not announced yet stays out of it whoever asks (#435), and no staff tools.
        var host = (direct ? ChatHost.DiscordDirect : ChatHost.Discord) with { Forms = FormsOn };
        var saveTo = direct ? null : context;
        var earlier = direct ? await DirectMessagesAsync(message, click is not null) : null;
        var history = earlier is null ? await ChatHistoryAsync(context, UserId, BotID) : AsHistory(earlier);

        // The tools, as the author: their linked site account and its roles, or no account (every tool not about "you").
        // Each tool call runs in a scope of its own: this scope's context saves the streamed reply meanwhile
        var caller = await new DiscordCaller(context).AsToolCaller(authorId);

        // The chat's limits, as on the site: the question's length, the history's, and the author's (their site account's when
        // linked, so they count alike here and on the site; else their Discord account's)
        ChatReply reply;
        var asker = ChatLimits.Asker(caller, ChatLimits.Discord(authorId));
        try
        {
            // In a DM, a yes or no answers the change waiting for the author (GrifChat.ConfirmAsync): anything else is a question
            if (click is not null)
                reply = await chat.ConfirmAsync(history, click.ActionId, click.Approved, caller, host, asker);
            else if (direct && Answer(question) is { } approved && await chat.WaitingAsync(host, asker) is { } waiting)
                reply = await chat.ConfirmAsync(history, waiting.Id, approved, caller, host, asker);
            // The model: in a DM, the one the author chose on the site (else their default); in a channel, the one anyone may use
            else
                reply = await chat.StartAsync(history, question, caller, host, asker);
        }
        catch (ChatRefusedException refusal)
        {
            // Neither the question nor why is kept: the model never had it (a DM's are left out of its history, GrifChat.WithoutRefusals)
            await RefuseAsync(message, refusal.Message, click, host, asker, earlier);
            if (refused is not null)
                await refused();
            return null;
        }
        catch (Exception e)
        {
            // The database failed as the reply started (the author's choice, their month): they are told, not left with no answer
            logger.LogError(e, "The chat failed to start answering Discord message {MessageId} from Discord user {DiscordUserId}", message.Id, authorId);
            await message.ReplyAsync(Chat.ChatHub.Failed);
            // Its type: its message may quote a user
            return e.GetType().Name;
        }
        using (reply)
        {
            if (saveTo is not null)
                await SaveQuestionAsync(saveTo, message, UserId, BotID);
            var cutShort = await AnswerAsync(message, reply, saveTo, UserId, BotID, new Asking(asker, direct));
            // A reply started, so the change waiting in the DM was answered or forgotten (GrifChat): its buttons go, once the reply is out
            await TakeOffAnsweredConfirmsAsync(earlier, keep: null);
            return cutShort;
        }
    }

    /// <summary>
    /// Why the chat won't answer, as the reply. A click's change that still waits (refused for the asker's limits or month, GrifChat.ConfirmAsync
    /// keeps it) gets its Confirm and Cancel again with it: the clicked ones are gone. In a DM (<paramref name="earlier"/>), the Confirm and
    /// Cancel of any other change go from the earlier messages: refused, it may have been dropped all the same (its model gone).
    /// </summary>
    private async Task RefuseAsync(NetCord.Rest.RestMessage message, string refusal, ChatClick? click, ChatHost host, string asker, List<IDiscordRestMessage>? earlier)
    {
        var waiting = earlier is null ? null : await chat.WaitingAsync(host, asker);
        var reply = new NetCord.Rest.ReplyMessageProperties().WithContent(refusal);
        if (click is not null && waiting?.Id == click.ActionId)
            reply = reply.WithComponents(ChatComponents.For([], waiting)!);
        await message.ReplyAsync(reply);
        await TakeOffAnsweredConfirmsAsync(earlier, waiting?.Id);
    }

    /// <summary>
    /// The reply shown as it streams in, then whole, and saved (to <paramref name="saveTo"/>, a channel's; none from a DM). A reply cut
    /// short (it took too long) is followed by why, which is returned.
    /// </summary>
    private async Task<string?> AnswerAsync(NetCord.Rest.RestMessage message, ChatReply reply, GrifballContext? saveTo, long UserId, long BotID, Asking asking)
    {
        // Each chunk with when it arrived: the reply is paced by the stream, not by when the background task gets to it
        var channel = Channel.CreateUnbounded<(string Text, long ArrivedAt)>();
        var responseBuilder = new StringBuilder();

        // Background task for incremental updates
        var started = Time.GetTimestamp();
        var pictures = new List<ChatImage>();
        var baseUrl = configuration["BaseUrl"] ?? "";
        var updateTask = Task.Run(() => StreamReplyAsync(channel.Reader, responseBuilder, message, saveTo, UserId, BotID, Time, started, pictures, baseUrl));

        string? cutShort = null;
        try
        {
            await foreach (var text in reply.StreamAsync())
                await channel.Writer.WriteAsync((text, Time.GetTimestamp()));
        }
        catch (ChatRefusedException refused)
        {
            cutShort = refused.Message;
        }
        finally
        {
            // Always complete the writer so background thread never hangs even if an error occurs
            channel.Writer.Complete();
        }
        var restMessage = await updateTask;

        // The pictures it names (get_player_image's addresses, in the earlier messages too) are attached to its last message, and the addresses left out of the text
        var rest = responseBuilder.ToString();
        pictures.AddRange(ChatImages.In(rest, baseUrl));
        responseBuilder.Clear().Append(ChatImages.Without(rest, baseUrl));
        var form = reply.OfferedForm is { } offered ? await HoldAsync(offered, asking) : ((string ID, string Title)?)null;
        var components = ChatComponents.For(reply.Offered, reply.Waiting, form);
        using var files = await PicturesAsync([.. pictures.Distinct()]);
        await FinishReplyAsync(message, restMessage, responseBuilder, saveTo, UserId, BotID, files?.Files, components);
        if (cutShort is not null)
            await message.ReplyAsync(cutShort);
        return cutShort;
    }

    /// <summary>The author and the bot, as Discord users in the database (a message is between two of them).</summary>
    private static async Task EnsureDiscordUsersAsync(GrifballContext context, long UserId, string userName, long BotID)
    {
        // Ensure the user exists in the database
        if (!await context.DiscordUsers.AnyAsync(x => x.DiscordUserID == UserId))
        {
            context.Add(Domain.Players.DiscordUser.FromDiscord(UserId, userName));
            await context.SaveChangesAsync();
        }

        if (!await context.DiscordUsers.AnyAsync(x => x.DiscordUserID == BotID))
        {
            context.Add(Domain.Players.DiscordUser.FromDiscord(BotID, "GrifballWebApp"));
            await context.SaveChangesAsync();
        }
    }

    /// <summary>The channels' conversation before the new message (its last <see cref="GrifChat.HistoryLimit"/> messages), oldest first.</summary>
    private static async Task<List<ChatMessage>> ChatHistoryAsync(GrifballContext context, long UserId, long BotID)
    {
        var messages = await context.DiscordMessages
            .Where(x => (x.ToDiscordUserId == UserId && x.FromDiscordUserId == BotID) ||
                        (x.ToDiscordUserId == BotID && x.FromDiscordUserId == UserId))
            .OrderByDescending(x => x.Timestamp)
            .Take(GrifChat.HistoryLimit)
            .ToListAsync();

        return messages
            .OrderBy(x => x.Timestamp) // Order oldest to newest
            .Select(x => new ChatMessage(x.FromDiscordUserId == BotID ? ChatRole.Assistant : ChatRole.User, x.Content))
            .ToList();
    }

    /// <summary>A question the chat took, kept for the channels' conversation.</summary>
    private static async Task SaveQuestionAsync(GrifballContext context, NetCord.Rest.RestMessage message, long UserId, long BotID)
    {
        var newMessage = new DiscordMessage()
        {
            Id = (long)message.Id,
            FromDiscordUserId = UserId,
            ToDiscordUserId = BotID,
            Content = message.Content,
            Timestamp = message.CreatedAt.UtcDateTime,
        };
        context.DiscordMessages.Add(newMessage);
        await context.SaveChangesAsync();
    }

    /// <summary>A DM's answer to a change waiting for its author: yes (true), no (false), or null for anything else, a question.</summary>
    public static bool? Answer(string content) => content.Trim().TrimEnd('.', '!').ToLowerInvariant() switch
    {
        "yes" or "y" or "confirm" or "do it" => true,
        "no" or "n" or "cancel" => false,
        _ => null,
    };

    /// <summary>
    /// A DM's conversation so far, as Discord keeps it in the DM channel: its last <see cref="GrifChat.HistoryLimit"/> messages before
    /// this one, oldest first. Never from DiscordMessages, the channels' conversation, which keeps none of a DM.
    /// </summary>
    private async Task<List<IDiscordRestMessage>> DirectMessagesAsync(NetCord.Rest.RestMessage message, bool includeMessage)
    {
        var earlier = await discordClient.GetMessagesAsync(message.ChannelId, new NetCord.Rest.PaginationProperties<ulong> { BatchSize = GrifChat.HistoryLimit + 1 })
            .Where(m => m.Id < message.Id || (includeMessage && m.Id == message.Id))
            .Take(GrifChat.HistoryLimit)
            .ToListAsync();
        return [.. earlier.OrderBy(m => m.Id)];
    }

    /// <summary>
    /// A DM's messages (<see cref="DirectMessagesAsync"/>) as the chat's history. The bot's are the model's, but a form's answers: the bot posts them
    /// as the response to the person's submit (<see cref="ChatFormHandler"/>), and they are the person's words.
    /// </summary>
    private List<ChatMessage> AsHistory(List<IDiscordRestMessage> messages)
        => [.. messages.Select(m => new ChatMessage(m.Author.Id == BotId && !IsFormAnswers(m) ? ChatRole.Assistant : ChatRole.User, m.Content))];

    private bool IsFormAnswers(IDiscordRestMessage message)
        => message.InteractionMetadata is { Type: NetCord.InteractionType.ModalSubmit } submit && submit.User.Id != BotId && ChatForms.AreAnswers(message.Content);

    /// <summary>
    /// Confirm and Cancel taken off the bot's earlier DM messages (<paramref name="earlier"/>; none in a channel), but those of <paramref name="keep"/>,
    /// a change still waiting: the buttons they offered are left as they were. A message that can't be changed now is logged: the reply goes on.
    /// </summary>
    private async Task TakeOffAnsweredConfirmsAsync(List<IDiscordRestMessage>? earlier, string? keep)
    {
        foreach (var answered in (earlier ?? []).Where(m => m.Author.Id == BotId && ChatComponents.HasConfirm(m.Components.Select(c => c.Original), keep)))
        {
            try
            {
                await answered.ModifyAsync(x => x.WithComponents(ChatComponents.WithoutConfirm(answered.Components.Select(c => c.Original), keep)));
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Could not take Confirm and Cancel off Discord message {MessageId}", answered.Id);
            }
        }
    }

    /// <summary>
    /// Shows the reply as it streams in (<paramref name="chunks"/>, each with when it arrived): every 25 characters at most once a second, each full
    /// 2000 characters as a message of its own (saved). Returns the message still being written, if any.
    /// </summary>
    private static async Task<NetCord.Rest.RestMessage?> StreamReplyAsync(ChannelReader<(string Text, long ArrivedAt)> chunks, StringBuilder responseBuilder, NetCord.Rest.RestMessage message,
        GrifballContext? context, long UserId, long BotID, TimeProvider time, long started, List<ChatImage> pictures, string baseUrl)
    {
        NetCord.Rest.RestMessage? restMessage = null;
        var timeThreshold = TimeSpan.FromSeconds(1);
        const int charThreshold = 25;
        int lastLength = 0;
        var lastUpdate = started;
        await foreach (var (chunk, arrivedAt) in chunks.ReadAllAsync())
        {
            responseBuilder.Append(chunk);
            bool timeElapsed = time.GetElapsedTime(lastUpdate, arrivedAt) > timeThreshold;
            bool charsAdded = responseBuilder.Length - lastLength >= charThreshold;
            if (charsAdded && timeElapsed) // Only update if enough time has passed or enough characters have been added
            {
                var currentResponse = responseBuilder.ToString();
                while (currentResponse.Length > 2000)
                {
                    var cut = CutAt(currentResponse);
                    var shown = Shown(currentResponse[..cut], pictures, baseUrl); // its pictures are kept for the last message
                    restMessage = await SendFullAsync(message, restMessage, shown, context, UserId, BotID);
                    restMessage = null; // Null out the message so we do not try to save or send it again
                    currentResponse = currentResponse[cut..]; // Get everything after the first 2000 characters
                    responseBuilder.Clear(); // Clear the response builder to avoid duplication
                    responseBuilder.Append(currentResponse); // Append the remaining characters to the builder
                }
                if (string.IsNullOrEmpty(currentResponse) is false)
                {
                    restMessage = await SendOrModify(message, restMessage, currentResponse);
                }
                lastUpdate = arrivedAt;
                lastLength = currentResponse.Length;
            }
        }
        return restMessage;
    }

    /// <summary>Where a full message of <paramref name="text"/> (over 2000 characters) ends: at 2000, or before a picture address that would be cut in two.</summary>
    private static int CutAt(string text)
    {
        var wordStart = text.LastIndexOfAny([' ', '\t', '\r', '\n'], 1999) + 1;
        var wordEnd = text.IndexOfAny([' ', '\t', '\r', '\n'], 2000);
        var word = text[wordStart..(wordEnd < 0 ? text.Length : wordEnd)];
        return wordStart > 0 && word.Contains("/api/users/") ? wordStart : 2000;
    }

    /// <summary>A part of the reply as shown: the pictures it names are added to <paramref name="pictures"/>, and their addresses left out.</summary>
    private static string Shown(string part, List<ChatImage> pictures, string baseUrl)
    {
        pictures.AddRange(ChatImages.In(part, baseUrl));
        return ChatImages.Without(part, baseUrl);
    }

    /// <summary>A full message of the reply, sent (or the one streamed so far made whole) and saved; nothing of it left (only pictures) is nothing to send.</summary>
    private static async Task<NetCord.Rest.RestMessage?> SendFullAsync(NetCord.Rest.RestMessage message, NetCord.Rest.RestMessage? restMessage, string text,
        GrifballContext? context, long UserId, long BotID)
    {
        if (text.Length == 0)
            return null;
        restMessage = await SendOrModify(message, restMessage, text);
        await SaveMessageToDb(UserId, BotID, context, restMessage);
        return restMessage;
    }

    /// <summary>What Discord takes on one message: ten files, and a safe share of its size limit.</summary>
    private const int MaxPictures = 10, MaxPictureBytes = 8_000_000;

    /// <summary>The files of a reply's pictures, and the streams behind them.</summary>
    private sealed class PictureFiles : IDisposable
    {
        public List<NetCord.Rest.AttachmentProperties> Files { get; } = [];
        private readonly List<MemoryStream> _streams = [];

        public void Add(string name, byte[] content)
        {
            var stream = new MemoryStream(content);
            _streams.Add(stream);
            Files.Add(new NetCord.Rest.AttachmentProperties(name, stream));
        }

        public void Dispose()
        {
            foreach (var stream in _streams)
                stream.Dispose();
        }
    }

    /// <summary>
    /// The pictures from the site's own store, as files to attach: only ones the site has stored, at most <see cref="MaxPictures"/> and
    /// <see cref="MaxPictureBytes"/> together. A picture that fails to load is left out: the reply still goes.
    /// </summary>
    private async Task<PictureFiles?> PicturesAsync(IReadOnlyList<ChatImage> pictures)
    {
        if (pictures.Count == 0)
            return null;
        var files = new PictureFiles();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var images = scope.ServiceProvider.GetRequiredService<XboxImageService>();
            var bytes = 0;
            foreach (var picture in pictures)
            {
                if (files.Files.Count >= MaxPictures)
                    break;
                if (!await ChatImages.StoredAsync(images, picture) || await ChatImages.LoadAsync(images, picture) is not { } content
                    || bytes + content.Content.Length > MaxPictureBytes)
                    continue;
                bytes += content.Content.Length;
                var extension = content.ContentType switch { "image/jpeg" => "jpg", "image/webp" => "webp", _ => "png" };
                files.Add($"{picture.UserID}-{ChatImages.Slug(picture.Kind)}.{extension}", content.Content);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "A picture of the reply failed to load: the reply goes without the rest");
        }
        if (files.Files.Count > 0)
            return files;
        files.Dispose();
        return null;
    }

    /// <summary>The whole reply shown (in messages of 2000 characters at most) and saved, once it has streamed in; <paramref name="files"/> go with its last message.</summary>
    private async Task FinishReplyAsync(NetCord.Rest.RestMessage message, NetCord.Rest.RestMessage? restMessage, StringBuilder responseBuilder,
        GrifballContext? context, long UserId, long BotID, List<NetCord.Rest.AttachmentProperties>? files = null, IReadOnlyList<NetCord.Rest.ActionRowProperties>? components = null)
    {
        // Final update to ensure the full response is shown
        var finalResponse = responseBuilder.ToString();

        while (finalResponse.Length > 2000)
        {
            var first2000 = finalResponse[..2000]; // Get the first 2000 characters, send it now
            restMessage = await SendOrModify(message, restMessage, first2000);
            await SaveMessageToDb(UserId, BotID, context, restMessage);
            restMessage = null; // Null out the message so we do not try to save or send it again
            finalResponse = finalResponse[2000..]; // Get everything after the first 2000 characters
            responseBuilder.Clear(); // Clear the response builder to avoid duplication
            responseBuilder.Append(finalResponse); // Append the remaining characters to the builder
        }

        await SendLastAsync(message, restMessage, finalResponse, context, UserId, BotID, files, components);
    }

    /// <summary>
    /// The last message of a reply: sent, or the one streamed so far made whole (with <paramref name="files"/>), and saved. Discord refusing
    /// the files (too many, too large) still gets the text sent.
    /// </summary>
    private async Task SendLastAsync(NetCord.Rest.RestMessage message, NetCord.Rest.RestMessage? restMessage, string text,
        GrifballContext? context, long UserId, long BotID, List<NetCord.Rest.AttachmentProperties>? files, IReadOnlyList<NetCord.Rest.ActionRowProperties>? components)
    {
        NetCord.Rest.RestMessage? sent;
        string? saved = null;
        try
        {
            sent = await SendFinalAsync(message, restMessage, text, files, components);
            if (sent is not null && files is not null)
                saved = ShownNote(text, files);
        }
        catch (Exception e) when (files is not null)
        {
            logger.LogWarning(e, "Discord refused the pictures of its reply to message {MessageId}: the text goes without them", message.Id);
            sent = await SendFinalAsync(message, restMessage, text, null, components);
        }
        if (sent is not null)
            await SaveMessageToDb(UserId, BotID, context, sent, saved);
    }

    /// <summary>The reply as kept for the conversation: its text, and a note of the pictures that went with it (the next turns see no files).</summary>
    private static string ShownNote(string text, List<NetCord.Rest.AttachmentProperties> files)
    {
        var note = $"[Pictures shown with this reply: {string.Join(", ", files.Select(f => f.FileName))}]";
        return text.Length == 0 ? note : $"{text}\n{note}";
    }

    /// <summary>The last message as sent, or as streamed (when it already shows all of it); null when there is nothing to send.</summary>
    private static async Task<NetCord.Rest.RestMessage?> SendFinalAsync(NetCord.Rest.RestMessage message, NetCord.Rest.RestMessage? restMessage, string text, List<NetCord.Rest.AttachmentProperties>? files,
        IReadOnlyList<NetCord.Rest.ActionRowProperties>? components)
    {
        if (restMessage is null)
        {
            // The text may be empty if the builder was cleared above
            if (string.IsNullOrEmpty(text) && files is null && components is null)
                return null;
            return await message.ReplyAsync(NewReply(text, files, components));
        }
        // The last streamed update may already show all of it; it is saved either way (only full messages are).
        if (restMessage.Content == text && files is null && components is null)
            return restMessage;
        return await restMessage.ModifyAsync(x =>
        {
            x.WithContent(text);
            if (files is not null)
                x.WithAttachments(files);
            if (components is not null)
                x.WithComponents(components);
        });
    }

    private static NetCord.Rest.ReplyMessageProperties NewReply(string text, List<NetCord.Rest.AttachmentProperties>? files, IReadOnlyList<NetCord.Rest.ActionRowProperties>? components)
    {
        var reply = new NetCord.Rest.ReplyMessageProperties();
        if (string.IsNullOrEmpty(text) is false)
            reply = reply.WithContent(text);
        if (files is not null)
            reply = reply.WithAttachments(files);
        return components is null ? reply : reply.WithComponents(components);
    }

    /// <summary>
    /// This method sends a response to message, or modifies an existing message.
    /// </summary>
    /// <param name="messageToRespondTo"></param>
    /// <param name="existingMessage"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    private static async Task<NetCord.Rest.RestMessage> SendOrModify(NetCord.Rest.RestMessage messageToRespondTo, NetCord.Rest.RestMessage? existingMessage, string content)
    {
        if (existingMessage is null)
        {
            return await messageToRespondTo.ReplyAsync(content);
        }
        else
        {
            return await existingMessage.ModifyAsync(x => x.WithContent(content));
        }
    }

    /// <summary>The bot's message, kept for the channel's conversation; none is kept from a DM (no <paramref name="context"/>).</summary>
    private static async Task SaveMessageToDb(long UserId, long BotID, GrifballContext? context, NetCord.Rest.RestMessage restMessage, string? content = null)
    {
        if (context is null)
            return;
        var newMessage = new DiscordMessage()
        {
            Id = (long)restMessage.Id,
            FromDiscordUserId = BotID,
            ToDiscordUserId = UserId,
            Content = content ?? restMessage.Content,
            Timestamp = restMessage.CreatedAt.UtcDateTime,
        };
        context.DiscordMessages.Add(newMessage);
        await context.SaveChangesAsync();
    }
}
