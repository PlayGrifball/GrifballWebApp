using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using OllamaSharp;

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

        var response = "";
        NetCord.Rest.RestMessage? restMessage = null;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastUpdate = stopwatch.Elapsed;
        int lastLength = 0;
        const int charThreshold = 25;
        var timeThreshold = TimeSpan.FromSeconds(1);

        var instructions = @"
You are a Discord bot, and all messages must stay under the 2000-character limit.
Your primary purpose is to provide stats and information about Grifball.
You currently support standard Discord slash commands.
In future updates, users should be able to interact with the bot using natural language commands directly (without the slash).
";

        await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory, new ChatOptions()
        {
            Instructions = instructions,
        }))
        {
            response += item.Text;
            bool timeElapsed = stopwatch.Elapsed - lastUpdate > timeThreshold;
            bool charsAdded = response.Length - lastLength >= charThreshold;

            if (restMessage is null || timeElapsed || charsAdded)
            {
                if (restMessage is null)
                {
                    restMessage = await message.ReplyAsync(response);
                }
                else
                {
                    restMessage = await restMessage.ModifyAsync(x => x.WithContent(response));
                }
                lastUpdate = stopwatch.Elapsed;
                lastLength = response.Length;
            }
        }

        // Final update to ensure the full response is shown
        if (restMessage is not null && restMessage.Content.Length != response.Length)
        {
            await restMessage.ModifyAsync(x => x.WithContent(response));
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
}
