using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using OllamaSharp;
using System.Text;
using System.Threading.Channels;

namespace GrifballWebApp.Server;

[GatewayEvent(nameof(GatewayClient.MessageCreate))]
public class MessageCreateHandler(IDiscordClient discordClient, IDbContextFactory<GrifballContext> contextFactory, IConfiguration configuration, IServiceScopeFactory scopeFactory) : IGatewayEventHandler<Message>
{
    private ulong BotId { get; } = ulong.Parse(configuration["Discord:ClientId"] ?? "0");

    public async ValueTask HandleAsync(Message message)
    {
        if (message.Author.Id == BotId)
            return;

        if (message.MentionedUsers.Any(x => x.Id == BotId))
        {
            await HandleResponse(message);
            return;
        }

        if (message.MessageReference is not null)
        {
            var replyToId = message.MessageReference.MessageId;
            var channelId = message.MessageReference.ChannelId;
            var replyToMessage = await discordClient.GetMessageAsync(channelId, replyToId);
            if (replyToMessage is null)
                return;
            if (replyToMessage.Author.Id == BotId)
            {
                await HandleResponse(message);
                return;
            }
        }
    }

    private async ValueTask HandleResponse(Message message)
    {
        IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434/"), "llama3.1:latest");

        chatClient = new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();


        var UserId = (long)message.Author.Id;
        var BotID = (long)BotId;
        using var context = await contextFactory.CreateDbContextAsync();

        // Ensure the user exists in the database
        if (!await context.DiscordUsers.AnyAsync(x => x.DiscordUserID == UserId))
        {
            context.Add(new DiscordUser()
            {
                DiscordUserID = UserId,
                DiscordUsername = message.Author.Username,
            });
            await context.SaveChangesAsync();
        }

        if (!await context.DiscordUsers.AnyAsync(x => x.DiscordUserID == BotID))
        {
            context.Add(new DiscordUser()
            {
                DiscordUserID = BotID,
                DiscordUsername = "GrifballWebApp",
            });
            await context.SaveChangesAsync();
        }

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

        var chatHistory = messages
            .Append(newMessage) // Add the new message to the history
            .OrderBy(x => x.Timestamp) // Order oldest to newest
            .Select(x => new ChatMessage(x.FromDiscordUserId == BotID ? ChatRole.Assistant : ChatRole.User, x.Content))
            .ToList();

        var instructions = @"
You are a Discord bot, and all messages must stay under the 2000-character limit.
Your primary purpose is to provide stats and information about Grifball.
You currently support standard Discord slash commands.
In future updates, users should be able to interact with the bot using natural language commands directly (without the slash).
";

        var channel = Channel.CreateUnbounded<string>();
        var responseBuilder = new StringBuilder();
        NetCord.Rest.RestMessage? restMessage = null;

        // Background task for incremental updates
        var updateTask = Task.Run(async () =>
        {
            var timeThreshold = TimeSpan.FromSeconds(1);
            const int charThreshold = 25;
            int lastLength = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var lastUpdate = stopwatch.Elapsed;
            await foreach (var chunk in channel.Reader.ReadAllAsync())
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
        });

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
        await updateTask;

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
        else if (restMessage.Content.Length != finalResponse.Length)
        {
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
            //_logger.LogError(ex, "Failed to fetch recent matches for {Gamertag}", xboxUser.Gamertag);
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
    public List<MatchTeamDTO> Teams { get; set; }
}

public class MatchTeamDTO
{
    public int Id { get; set; }
    public int Score { get; set; }
    public Outcomes Outcome { get; set; }
    public List<string> Players { get; set; }
}
