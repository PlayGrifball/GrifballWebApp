using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using OllamaSharp;
using System.Text;
using System.Threading.Channels;

namespace GrifballWebApp.Server;

[GatewayEvent(nameof(GatewayClient.MessageCreate))]
public class MessageCreateHandler(IDiscordClient discordClient, IDbContextFactory<GrifballContext> contextFactory, IConfiguration configuration) : IGatewayEventHandler<Message>
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
                if ((charsAdded && timeElapsed)) // Only update if enough time has passed or enough characters have been added
                {
                    var currentResponse = responseBuilder.ToString();
                    if (restMessage is null)
                    {
                        restMessage = await message.ReplyAsync(currentResponse);
                    }
                    else
                    {
                        restMessage = await restMessage.ModifyAsync(x => x.WithContent(currentResponse));
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

        if (restMessage is null)
        {
            restMessage = await message.ReplyAsync(finalResponse);
        }
        else if (restMessage.Content.Length != finalResponse.Length)
        {
            restMessage = await restMessage.ModifyAsync(x => x.WithContent(finalResponse));
        }

        var newMessage2 = new DiscordMessage()
        {
            Id = (long)restMessage.Id,
            FromDiscordUserId = BotID,
            ToDiscordUserId = UserId,
            Content = restMessage.Content,
            Timestamp = restMessage.CreatedAt.UtcDateTime,
        };
        context.DiscordMessages.Add(newMessage2);
        await context.SaveChangesAsync();
    }
}
