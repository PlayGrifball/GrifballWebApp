using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
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

    [ComponentInteraction("voteforwinner")]
    public async Task VoteForWinner(int matchId, string winner)
    {
        var parsed = Enum.TryParse<WinnerVote>(winner, out var outValue);
        if (parsed is false)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"I could not parse the value {winner}. Contact developer, expected values are {string.Join(',', Enum.GetValues<WinnerVote>())}",
            }));
            return;
        }

        var match = await _context.MatchedMatches
            .Include(x => x.HomeTeam)
                .ThenInclude(x => x.Players)
                    .ThenInclude(x => x.DiscordUser)
            .Include(x => x.AwayTeam)
                .ThenInclude(x => x.Players)
                    .ThenInclude(x => x.DiscordUser)
            .Where(x => x.Active)
            .FirstOrDefaultAsync(x => x.Id == matchId);

        if (match is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"Match does not exist or it is no longer active",
            }));
            return;
        }

        var discordIDs = match.HomeTeam.Players
            .Select(x => x.DiscordUser.DiscordUserID)
            .Union(match.AwayTeam.Players.Select(x => x.DiscordUser.DiscordUserID))
            .ToList();

        if (discordIDs.Contains((long)Context.User.Id) is false)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You are not allowed to vote since you are not in this match",
            }));
            return;
        }

        var vote = await _context.MatchedWinnerVote
            .FirstOrDefaultAsync(x => x.DiscordUser.DiscordUserID == (long)Context.User.Id && x.MatchId == matchId);

        if (vote is null)
        {
            vote = new MatchedWinnerVote();
            _context.MatchedWinnerVote.Add(vote);
        }
        vote.DiscordUserId = (long)Context.User.Id;
        vote.MatchId = matchId;
        vote.WinnerVote = outValue;
        await _context.SaveChangesAsync();

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = $"Thanks for voting! You voted for {outValue}",
        }));
    }
}
