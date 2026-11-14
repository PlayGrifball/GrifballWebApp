using System.Security.Claims;
using System.Threading.Channels;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server.Mcp;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using TypedSignalR.Client;

namespace GrifballWebApp.Server.Chat;

/// <summary>
/// The site's chat: the Discord bot's (<see cref="GrifChat"/>, on <see cref="ChatHost.Site"/>), its reply streamed to the browser as
/// it is written. A signed-in user asks as themselves, with their roles; signed out, as a Discord user with no linked account
/// does (<see cref="ToolCaller.Anonymous"/>). The browser keeps the conversation (its tab's session) and sends it with each
/// question: the server keeps none. Limits: <see cref="ChatLimits"/>, and <see cref="GrifChat"/>'s.
/// </summary>
public class ChatHub(GrifChat chat, ChatLimits limits, IAmbientCurrentUser ambientCurrentUser, ILogger<ChatHub> logger) : Hub, IChatHubServer
{
    /// <summary>Where it is mapped (the browser reaches it at hub/ChatHub).</summary>
    public const string Path = "ChatHub";

    /// <summary>The largest message a browser may send it: a question with its history, every message at its limit.</summary>
    public const long MaximumReceiveMessageSize = 256 * 1024;

    public const string TookTooLong = "That took too long to answer: ask again, or ask something simpler.";
    public const string Failed = "The chat couldn't answer just now: try again later.";

    public Task<ChannelReader<string>> Ask(ChatRequestDto request, CancellationToken cancellationToken)
    {
        var reply = Channel.CreateUnbounded<string>();
        if (GrifChat.Refusal(request) is { } refused)
            return Refuse(reply, refused);
        var conversation = GrifChat.Conversation(request);
        var caller = Caller(Context.User);
        var connectionId = Context.ConnectionId;
        if (limits.TryStart(connectionId, ChatLimits.Partition(caller, Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString())) is { } limited)
            return Refuse(reply, limited);

        // On its own: the reply streams after this returns, and the ambient user is set for it alone
        _ = Task.Run(() => ReplyAsync(reply.Writer, conversation, caller, connectionId, cancellationToken), CancellationToken.None);
        return Task.FromResult(reply.Reader);
    }

    /// <summary>
    /// A refusal, as the stream's error the browser shows: not thrown, so the hubs' error log (ExceptionLogHubFilter) doesn't take
    /// a question asked too often for a fault.
    /// </summary>
    private static Task<ChannelReader<string>> Refuse(Channel<string> reply, string why)
    {
        reply.Writer.TryComplete(new HubException(why));
        return Task.FromResult(reply.Reader);
    }

    /// <summary>The caller a connection's user is: the signed-in user with their roles, else no account.</summary>
    public static ToolCaller Caller(ClaimsPrincipal? user)
        => user?.Identity?.IsAuthenticated == true ? GrifMcp.Caller(user) : ToolCaller.Anonymous;

    /// <summary>
    /// The model's reply, written to <paramref name="reply"/> as it comes, then completed: with a <see cref="HubException"/> the
    /// browser shows when it took too long or failed (the failure logged), with nothing when the browser stopped reading.
    /// </summary>
    private async Task ReplyAsync(ChannelWriter<string> reply, List<ChatMessage> conversation, ToolCaller caller, string connectionId, CancellationToken cancellationToken)
    {
        // The caller is the ambient user, for the tools' own scopes (a queued pull is theirs), as the Discord bot sets the author
        ambientCurrentUser.Begin();
        Exception? error = null;
        try
        {
            ambientCurrentUser.UserId = caller.UserID;
            await foreach (var text in chat.ReplyAsync(conversation, caller, ChatHost.Site, cancellationToken))
            {
                if (text.Length > 0)
                    await reply.WriteAsync(text, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The browser stopped reading (asked to stop, or went): no one to tell
        }
        catch (OperationCanceledException)
        {
            error = new HubException(TookTooLong);
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
            limits.Finish(connectionId);
            reply.TryComplete(error);
        }
    }
}

/// <summary>What the browser calls on <see cref="ChatHub"/> (the typed client's hub proxy).</summary>
[Hub]
public interface IChatHubServer
{
    /// <summary>
    /// The chat's answer to a question, streamed as it is written. Refused (a HubException saying why) when the question is empty or
    /// too long, a reply is already under way on this connection, or the user has asked too many this minute.
    /// </summary>
    Task<ChannelReader<string>> Ask(ChatRequestDto request, CancellationToken cancellationToken);
}
