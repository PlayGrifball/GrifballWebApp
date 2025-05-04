using GrifballWebApp.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

public class ButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IQueueService _queryService;
    private readonly IPublisher _publisher;
    private readonly GrifballContext _context;

    public ButtonInteractions(IQueueService queryService, IPublisher publisher, GrifballContext context)
    {
        _queryService = queryService;
        _publisher = publisher;
        _context = context;
    }

    [ComponentInteraction("join_queue")]
    public async Task JoinQueue()
    {
        var discordUser = await _context.DiscordUsers
            .FirstOrDefaultAsync(x => x.DiscordUserID == (long)Context.User.Id);
        if (discordUser is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "You must set your gamertag first",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        var queuePlayer = await _queryService.GetQueuePlayer(Context.User.Id);
        var inMatch = await _queryService.IsInMatch(Context.User.Id);

        if (queuePlayer is not null || inMatch)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = queuePlayer is not null ? "You are already in the queue!" : "You are already in a match!",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
        }
        else
        {
            await _queryService.AddPlayerToQueue(Context.User.Id);

            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "You have joined the queue! 🎉",
                Flags = MessageFlags.Ephemeral,
            }));
            _ = _publisher.Publish(new UpdateDisplayNotification());
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
        }
    }

    [ComponentInteraction("leave_queue")]
    public async Task LeaveQueue()
    {
        var discordUser = await _context.DiscordUsers
            .FirstOrDefaultAsync(x => x.DiscordUserID == (long)Context.User.Id);
        if (discordUser is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "You must set your gamertag first",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        var queuePlayer = await _queryService.GetQueuePlayer(Context.User.Id);

        if (queuePlayer is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "You are not in the matchmaking queue.",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
        }
        else
        {
            await _queryService.RemovePlayerToQueue(Context.User.Id);
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "You have left the queue",
                Flags = MessageFlags.Ephemeral,
            }));
            _ = _publisher.Publish(new UpdateDisplayNotification());
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
        }
    }
}
