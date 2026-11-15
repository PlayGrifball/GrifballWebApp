using System.Threading.Channels;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using TypedSignalR.Client;

namespace GrifballWebApp.Server.Chat;

/// <summary>
/// The site's chat: the Discord bot's (<see cref="GrifChat"/>, on <see cref="ChatHost.Site"/>), its reply streamed to the browser as
/// it is written. A signed-in user asks as themselves, with their roles as they are now (<see cref="HubUser"/>, checked for every
/// call: a role lost is a staff tool lost); signed out, as a Discord user with no linked account does
/// (<see cref="ToolCaller.Anonymous"/>). The browser keeps the conversation (its tab's session) and sends it with each question:
/// the server keeps none. Limits: <see cref="GrifChat"/>'s, for the user, else for their address (<see cref="ChatLimits"/>).
/// </summary>
public class ChatHub(GrifChat chat, HubUser user, IAmbientCurrentUser ambientCurrentUser, ILogger<ChatHub> logger) : Hub<IChatHubClient>, IChatHubServer
{
    /// <summary>Where it is mapped (the browser reaches it at hub/ChatHub).</summary>
    public const string Path = "ChatHub";

    /// <summary>The most bytes a character can take in a question's JSON: a control character, escaped (\u0001).</summary>
    public const int MaxBytesPerCharacter = 6;

    /// <summary>
    /// The largest message a browser may send it: a question and the history the server reads, every message at its limit and every
    /// character at its largest in JSON (<see cref="MaxBytesPerCharacter"/>), and room for the rest of the message. The page sends no
    /// more (it trims as the server does), so a conversation however long is answered, never the connection dropped.
    /// </summary>
    public const long MaximumReceiveMessageSize =
        (GrifChat.HistoryLimit * (long)GrifChat.MaxHistoryMessageLength + GrifChat.MaxQuestionLength) * MaxBytesPerCharacter + 64 * 1024;

    public const string Failed = "The chat couldn't answer just now: try again later.";

    public async Task<ChannelReader<string>> Ask(ChatRequestDto request, CancellationToken cancellationToken)
    {
        var reply = Channel.CreateUnbounded<string>();
        if (GrifChat.Refusal(request) is { } refused)
            return Refuse(reply, refused);
        var caller = user.AsToolCaller();
        ChatReply started;
        try
        {
            started = await chat.StartAsync(GrifChat.History(request), request.Question, caller, ChatHost.Site,
                ChatLimits.Asker(caller, ChatLimits.Address(Context.GetHttpContext()?.Connection.RemoteIpAddress)), request.Model,
                // The page's Stop cancels it: the user may ask again at once
                cancellationToken);
        }
        catch (ChatRefusedException e)
        {
            return Refuse(reply, e.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped while the reply started (reading the user's choice and month): no one to tell, nothing failed
            reply.Writer.TryComplete();
            return reply.Reader;
        }
        catch (Exception e)
        {
            // The database failed as the reply started: the browser is told, as for a reply that fails, not given SignalR's own error
            logger.LogError(e, "The site's chat failed to start answering user {UserId}", caller.UserID);
            return Refuse(reply, Failed);
        }

        // On its own: the reply streams after this returns, and the ambient user is set for it alone
        _ = Task.Run(() => ReplyAsync(reply.Writer, started, caller, cancellationToken), CancellationToken.None);
        return reply.Reader;
    }

    /// <summary>The models the user may use here, the one their questions go to, and their spend this month (<see cref="GrifChat.ModelsAsync"/>).</summary>
    public Task<ChatModelsDto> Models() => chat.ModelsAsync(user.AsToolCaller(), Context.ConnectionAborted);

    /// <summary>
    /// The user chose <paramref name="model"/> for their questions, here and in a Discord DM: their models then, as <see cref="Models"/>.
    /// Refused (a HubException) when they aren't signed in, or may not use it.
    /// </summary>
    public async Task<ChatModelsDto> Choose(string model)
    {
        var caller = user.AsToolCaller();
        try
        {
            await chat.ChooseAsync(caller, model, Context.ConnectionAborted);
        }
        catch (ChatRefusedException e)
        {
            throw new HubException(e.Message);
        }
        return await chat.ModelsAsync(caller, Context.ConnectionAborted);
    }

    /// <summary>
    /// A refusal, as the stream's error the browser shows: not thrown, so the hubs' error log (ExceptionLogHubFilter) doesn't take
    /// a question asked too often for a fault.
    /// </summary>
    private static ChannelReader<string> Refuse(Channel<string> reply, string why)
    {
        reply.Writer.TryComplete(new HubException(why));
        return reply.Reader;
    }

    /// <summary>
    /// The model's reply, written to <paramref name="reply"/> as it comes, then completed: with a <see cref="HubException"/> the
    /// browser shows when it took too long or failed (the failure logged), with nothing when the browser stopped reading.
    /// </summary>
    private async Task ReplyAsync(ChannelWriter<string> reply, ChatReply started, ToolCaller caller, CancellationToken cancellationToken)
    {
        // The caller is the ambient user, for the tools' own scopes (a queued pull is theirs), as the Discord bot sets the author
        ambientCurrentUser.Begin();
        Exception? error = null;
        try
        {
            ambientCurrentUser.UserId = caller.UserID;
            await foreach (var text in started.StreamAsync(cancellationToken))
            {
                if (text.Length > 0)
                    await reply.WriteAsync(text, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The browser stopped reading (asked to stop, or went): no one to tell
        }
        catch (ChatRefusedException e)
        {
            error = new HubException(e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "The site's chat failed to answer user {UserId}", caller.UserID);
            error = new HubException(Failed);
        }
        finally
        {
            ambientCurrentUser.End();
            // Free first: the browser may ask again the moment the reply ends
            started.Dispose();
            reply.TryComplete(error);
        }
    }
}

/// <summary>What the browser calls on <see cref="ChatHub"/> (the typed client's hub proxy).</summary>
[Hub]
public interface IChatHubServer
{
    /// <summary>
    /// The chat's answer to a question, streamed as it is written. Refused (a HubException saying why) when there is no model, the
    /// question is empty or too long, the conversation can't be read, a reply is already under way for the user, or they have asked too
    /// many this minute, or the model asked for isn't theirs, or a paid model's monthly limit is reached.
    /// </summary>
    Task<ChannelReader<string>> Ask(ChatRequestDto request, CancellationToken cancellationToken);

    /// <summary>The models the user may use, the one their questions go to, and their spend on paid ones this month.</summary>
    Task<ChatModelsDto> Models();

    /// <summary>The user chose a model for their questions (here and in a Discord DM); refused when they may not use it.</summary>
    Task<ChatModelsDto> Choose(string model);
}

// S4023: empty on purpose, the hub sends nothing but its streams; the typed client and the SignalR page need it to know the hub
#pragma warning disable S4023
/// <summary>Methods <see cref="ChatHub"/> invokes on the browser: none (each answer is the stream its question returns).</summary>
[Receiver]
public interface IChatHubClient;
#pragma warning restore S4023
