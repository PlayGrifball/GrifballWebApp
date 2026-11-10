using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using System.Text;
using System.Threading.Channels;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Discord;

namespace GrifballWebApp.Server;

public class MessageCreateHandler(IDiscordRestClient discordClient, IConfiguration configuration, IServiceScopeFactory scopeFactory, IAmbientCurrentUser ambientCurrentUser, ILogger<MessageCreateHandler> logger,
    IBotChatClients chatClients) : IMessageCreateGatewayHandler
{
    private ulong BotId { get; } = ulong.Parse(configuration["Discord:ClientId"] ?? "0");

    public async ValueTask HandleAsync(Message arg)
    {
        if (arg.Author.Id == BotId)
            return;

        if (arg.MentionedUsers.Any(x => x.Id == BotId))
        {
            await HandleResponse(arg);
            return;
        }

        if (arg.MessageReference is not null)
        {
            var replyToId = arg.MessageReference.MessageId;
            var channelId = arg.MessageReference.ChannelId;
            var replyToMessage = await discordClient.GetMessageAsync(channelId, replyToId);
            if (replyToMessage is null)
                return;
            if (replyToMessage.Author.Id == BotId)
            {
                await HandleResponse(arg);
                return;
            }
        }
    }

    private async ValueTask HandleResponse(Message message)
    {
        // A fresh ambient user slot per message, filled in with the author in HandleResponseCore, for contexts with no scope of
        // their own (IDbContextFactory) and other scopes (RecentMatches)
        ambientCurrentUser.Begin();
        try
        {
            await HandleResponseCore(message);
        }
        catch (Exception ex)
        {
            // One failing message must not fault the gateway handler for every message after it.
            logger.LogError(ex, "Failed to handle message {MessageId} from Discord user {DiscordUserId}", message.Id, message.Author.Id);
        }
        finally
        {
            ambientCurrentUser.End();
        }
    }

    private async ValueTask HandleResponseCore(Message message)
    {
        var chatClient = chatClients.Create();


        var UserId = (long)message.Author.Id;
        var BotID = (long)BotId;
        // The author's linked app user, if any, is the audit user for this scope's writes and the ambient user.
        using var scope = scopeFactory.CreateScope();
        await DiscordInteractionPipeline.SetCurrentUserAsync(message.Author.Id, logger, scope.ServiceProvider);
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();

        await EnsureDiscordUsersAsync(context, UserId, message.Author.Username, BotID);
        var chatHistory = await ChatHistoryAsync(context, message, UserId, BotID);

        var instructions = @"
You are a Discord bot, and all messages must stay under the 2000-character limit.
Your primary purpose is to provide stats and information about Grifball.
You currently support standard Discord slash commands.
In future updates, users should be able to interact with the bot using natural language commands directly (without the slash).
";

        var channel = Channel.CreateUnbounded<string>();
        var responseBuilder = new StringBuilder();

        // Background task for incremental updates
        var updateTask = Task.Run(() => StreamReplyAsync(channel.Reader, responseBuilder, message, context, UserId, BotID));

        try
        {
            await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory, new ChatOptions()
            {
                Instructions = instructions,

                Tools =
                [
                    AIFunctionFactory.Create(GetTime),
                    AIFunctionFactory.Create(RecentMatches),
                    AIFunctionFactory.Create(RecentMatchesUrl, null, "This method is used to get a waypoint url by a match id and gamertag. Do not ever remove %20 from the final response if it is present in the result."),
                ],
            }))
            {
                await channel.Writer.WriteAsync(item.Text);
            }
        }
        finally
        {
            // Always complete the writer so background thread never hangs even if an error occurs
            channel.Writer.Complete();
        }
        var restMessage = await updateTask;

        await FinishReplyAsync(message, restMessage, responseBuilder, context, UserId, BotID);
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

    /// <summary>The conversation so far (its last 25 messages), oldest first, the new message saved and last.</summary>
    private static async Task<List<ChatMessage>> ChatHistoryAsync(GrifballContext context, Message message, long UserId, long BotID)
    {
        var messages = await context.DiscordMessages
            .Where(x => (x.ToDiscordUserId == UserId && x.FromDiscordUserId == BotID) ||
                        (x.ToDiscordUserId == BotID && x.FromDiscordUserId == UserId))
            .OrderByDescending(x => x.Timestamp)
            .Take(25)
            .ToListAsync();

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

        return messages
            .Append(newMessage) // Add the new message to the history
            .OrderBy(x => x.Timestamp) // Order oldest to newest
            .Select(x => new ChatMessage(x.FromDiscordUserId == BotID ? ChatRole.Assistant : ChatRole.User, x.Content))
            .ToList();
    }

    /// <summary>
    /// Shows the reply as it streams in (<paramref name="chunks"/>): every 25 characters at most once a second, each full
    /// 2000 characters as a message of its own (saved). Returns the message still being written, if any.
    /// </summary>
    private static async Task<NetCord.Rest.RestMessage?> StreamReplyAsync(ChannelReader<string> chunks, StringBuilder responseBuilder, Message message,
        GrifballContext context, long UserId, long BotID)
    {
        NetCord.Rest.RestMessage? restMessage = null;
        var timeThreshold = TimeSpan.FromSeconds(1);
        const int charThreshold = 25;
        int lastLength = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastUpdate = stopwatch.Elapsed;
        await foreach (var chunk in chunks.ReadAllAsync())
        {
            responseBuilder.Append(chunk);
            bool timeElapsed = stopwatch.Elapsed - lastUpdate > timeThreshold;
            bool charsAdded = responseBuilder.Length - lastLength >= charThreshold;
            if (charsAdded && timeElapsed) // Only update if enough time has passed or enough characters have been added
            {
                var currentResponse = responseBuilder.ToString();
                while (currentResponse.Length > 2000)
                {
                    var first2000 = currentResponse[..2000]; // Get the first 2000 characters, send it now
                    restMessage = await SendOrModify(message, restMessage, first2000);
                    await SaveMessageToDb(UserId, BotID, context, restMessage);
                    restMessage = null; // Null out the message so we do not try to save or send it again
                    currentResponse = currentResponse[2000..]; // Get everything after the first 2000 characters
                    responseBuilder.Clear(); // Clear the response builder to avoid duplication
                    responseBuilder.Append(currentResponse); // Append the remaining characters to the builder
                }
                if (string.IsNullOrEmpty(currentResponse) is false)
                {
                    restMessage = await SendOrModify(message, restMessage, currentResponse);
                }
                lastUpdate = stopwatch.Elapsed;
                lastLength = currentResponse.Length;
            }
        }
        return restMessage;
    }

    /// <summary>The whole reply shown (in messages of 2000 characters at most) and saved, once it has streamed in.</summary>
    private static async Task FinishReplyAsync(Message message, NetCord.Rest.RestMessage? restMessage, StringBuilder responseBuilder,
        GrifballContext context, long UserId, long BotID)
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

        if (restMessage is null)
        {
            if (string.IsNullOrEmpty(finalResponse) is false)
            {
                // The finalResponse may be empty if we cleared the responseBuilder above
                restMessage = await message.ReplyAsync(finalResponse);
                await SaveMessageToDb(UserId, BotID, context, restMessage);
            }
            
        }
        else
        {
            // The last streamed update may already show all of it; it is saved either way (only full messages are).
            if (restMessage.Content != finalResponse)
                restMessage = await restMessage.ModifyAsync(x => x.WithContent(finalResponse));
            await SaveMessageToDb(UserId, BotID, context, restMessage);
        }
    }

    /// <summary>
    /// This method sends a response to message, or modifies an existing message.
    /// </summary>
    /// <param name="messageToRespondTo"></param>
    /// <param name="existingMessage"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    private static async Task<NetCord.Rest.RestMessage> SendOrModify(Message messageToRespondTo, NetCord.Rest.RestMessage? existingMessage, string content)
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

    private static async Task SaveMessageToDb(long UserId, long BotID, GrifballContext context, NetCord.Rest.RestMessage restMessage)
    {
        var newMessage = new DiscordMessage()
        {
            Id = (long)restMessage.Id,
            FromDiscordUserId = BotID,
            ToDiscordUserId = UserId,
            Content = restMessage.Content,
            Timestamp = restMessage.CreatedAt.UtcDateTime,
        };
        context.DiscordMessages.Add(newMessage);
        await context.SaveChangesAsync();
    }

    private string GetTime() => DateTime.UtcNow.DiscordTimeEmbed();

    public string RecentMatchesUrl(string xboxGamertag, Guid matchId)
    {
        var url = $"https://www.halowaypoint.com/halo-infinite/players/{Uri.EscapeDataString(xboxGamertag)}/matches/{matchId}";
        return url;
    }

    private async Task<List<MatchDTO>> RecentMatches(string xboxGamertag, int matchCount = 5)
    {
        using var scope = scopeFactory.CreateScope();
        var xboxUserService = scope.ServiceProvider.GetRequiredService<IGetsertXboxUserService>();

        var x = await xboxUserService.GetsertXboxUserByGamertag(xboxGamertag);

        if (x.Item1 is null)
            throw new InvalidOperationException(x.Item2);

        var xboxUser = x.Item1;

        var _dataPullService = scope.ServiceProvider.GetRequiredService<IDataPullService>();
        var _context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        try
        {
            await _dataPullService.DownloadRecentMatchesForPlayers([xboxUser.XboxUserID], startPage: 0, endPage: 0, matchCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch recent matches for {Gamertag}", xboxUser.Gamertag);
        }

        var matches = await _context.Matches
            .Where(x => x.MatchTeams.Any(x => x.MatchParticipants.Any(x => x.XboxUserID == xboxUser.XboxUserID)))
            .OrderByDescending(x => x.StartTime)
            .Take(matchCount)
            .Select(x => new MatchDTO
            {
                Id = x.MatchID,
                StartTime = x.StartTime ?? DateTime.MinValue,
                Teams = x.MatchTeams.Select(t => new MatchTeamDTO
                {
                    Id = t.TeamID,
                    Score = t.Score,
                    Outcome = t.Outcome,
                    Players = t.MatchParticipants.Select(p => p.XboxUser.Gamertag).ToList()
                }).ToList()
            })
            .AsNoTracking()
            .ToListAsync();
        return matches;
    }
}

public class MatchDTO
{
    public Guid Id { get; set; }
    public DateTime? StartTime { get; set; }
    public List<MatchTeamDTO> Teams { get; set; } = [];
}

public class MatchTeamDTO
{
    public int Id { get; set; }
    public int Score { get; set; }
    public Outcomes Outcome { get; set; }
    public List<string> Players { get; set; } = [];
}
