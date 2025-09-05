using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

public class ButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IQueueRepository _queryService;
    private readonly IPublisher _publisher;
    private readonly GrifballContext _context;
    private readonly IDiscordRestClient _discordClient;
    private readonly IOptions<DiscordOptions> _discordOptions;
    private readonly QueueService _displayQueueService;

    public ButtonInteractions(IQueueRepository queryService, IPublisher publisher, GrifballContext context, IDiscordRestClient discordClient, IOptions<DiscordOptions> discordOptions, QueueService displayQueueService)
    {
        _queryService = queryService;
        _publisher = publisher;
        _context = context;
        _discordClient = discordClient;
        _discordOptions = discordOptions;
        _displayQueueService = displayQueueService;
    }

    private async Task<Database.Models.User?> UserGuard()
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)Context.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await Context.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    [ComponentInteraction(DiscordButtonContants.SetGamertag)]
    public async Task SetGamertag()
    {
        var modal = new ModalProperties(DiscordModalsContants.SetGamertag, "Set Gamertag")
            .AddComponents(new TextInputProperties("gamertag", TextInputStyle.Short, "Gamertag")
            {
                Required = true,
                MaxLength = 20,
            });

        
       await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }


    [ComponentInteraction(DiscordButtonContants.JoinQueue)]
    public async Task JoinQueue()
    {
        var user = await UserGuard();
        if (user is null) return;

        var queuePlayer = await _queryService.GetQueuePlayer(user.Id);
        var inMatch = await _queryService.IsInMatch(user.Id);

        if (queuePlayer is not null || inMatch)
        {
            await Context.TempResponse(queuePlayer is not null ? "You are already in the queue!" : "You are already in a match!");
        }
        else
        {
            await _queryService.AddPlayerToQueue(user.Id);

            _ = _publisher.Publish(new UpdateDisplayNotification());
            await Context.TempResponse("You have joined the queue! 🎉");
        }
    }

    [ComponentInteraction(DiscordButtonContants.LeaveQueue)]
    public async Task LeaveQueue()
    {
        var user = await UserGuard();
        if (user is null) return;

        var queuePlayer = await _queryService.GetQueuePlayer(user.Id);

        if (queuePlayer is null)
        {
            await Context.TempResponse("You are not in the matchmaking queue.");
        }
        else
        {
            await _queryService.RemovePlayerToQueue(user.Id);
            _ = _publisher.Publish(new UpdateDisplayNotification());
            await Context.TempResponse("You have left the queue");
        }
    }

    [ComponentInteraction(DiscordButtonContants.VoteForWinner)]
    public async Task VoteForWinner(int matchId, string winner)
    {
        var user = await UserGuard();
        if (user is null) return;

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
                    .ThenInclude(x => x.User)
            .Include(x => x.AwayTeam)
                .ThenInclude(x => x.Players)
                    .ThenInclude(x => x.User)
            .Where(x => x.Active)
            .FirstOrDefaultAsync(x => x.Id == matchId);

        if (match is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"Match does not exist or it is no longer active",
                Flags = MessageFlags.Ephemeral,
            }));
            return;
        }

        var me = match.HomeTeam.Players
            .Union(match.AwayTeam.Players)
            .FirstOrDefault(x => x.UserID == user.Id);

        if (me is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You are not allowed to vote since you are not in this match",
                Flags = MessageFlags.Ephemeral,
            }));
            return;
        }
        else if (me.Kicked)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You are not allowed to vote since you have been kicked",
                Flags = MessageFlags.Ephemeral,
            }));
            return;
        }

        var vote = await _context.MatchedWinnerVotes
            .FirstOrDefaultAsync(x => x.MatchedPlayerId == me.Id && x.MatchId == matchId);

        if (vote is null)
        {
            vote = new MatchedWinnerVote();
            _context.MatchedWinnerVotes.Add(vote);
        }
        vote.MatchedPlayerId = me.Id;
        vote.MatchId = matchId;
        vote.WinnerVote = outValue;
        await _context.SaveChangesAsync();

        await _displayQueueService.UpdateThreadMessage(match, _context, _discordClient);

        await Context.TempResponse($"Thanks for voting! You voted for {outValue}");
    }
}
