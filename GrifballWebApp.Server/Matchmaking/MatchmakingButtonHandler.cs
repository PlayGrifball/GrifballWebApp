using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Notifications;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// The matchmaking buttons. <see cref="ButtonInteractions"/> is the NetCord module; it only wraps its context and calls these,
/// so they take our <see cref="IDiscordButtonInteractionContext"/> and tests can pass a substitute.
/// </summary>
public class MatchmakingButtonHandler
{
    private readonly IQueueRepository _queryService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GrifballContext _context;
    private readonly IDiscordRestClient _discordClient;
    private readonly QueueService _displayQueueService;
    private readonly ILogger<MatchmakingButtonHandler> _logger;

    public MatchmakingButtonHandler(IQueueRepository queryService, IServiceScopeFactory scopeFactory, GrifballContext context, IDiscordRestClient discordClient, QueueService displayQueueService, ILogger<MatchmakingButtonHandler> logger)
    {
        _queryService = queryService;
        _scopeFactory = scopeFactory;
        _context = context;
        _discordClient = discordClient;
        _displayQueueService = displayQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the queue display (and makes matches) without holding up the reply. It runs in its own scope: the
    /// interaction's scope, and its DbContext, are disposed once the reply is sent.
    /// </summary>
    public Task RefreshQueueDisplay()
    {
        return Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new UpdateDisplayNotification());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh the matchmaking queue display");
            }
        });
    }

    private async Task<Database.Models.User?> UserGuard(IDiscordButtonInteractionContext discordContext)
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)discordContext.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await discordContext.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    public async Task SetGamertag(IDiscordButtonInteractionContext discordContext)
    {
        var modal = new ModalProperties(DiscordModalsContants.SetGamertag, "Set Gamertag")
        {
            new LabelProperties("Gamertag", new TextInputProperties("gamertag", TextInputStyle.Short)
            {
                Required = true,
                MaxLength = 20,
            }),
        };

        await discordContext.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }


    public async Task JoinQueue(IDiscordButtonInteractionContext discordContext)
    {
        var user = await UserGuard(discordContext);
        if (user is null) return;

        var queuePlayer = await _queryService.GetQueuePlayer(user.Id);
        var inMatch = await _queryService.IsInMatch(user.Id);

        if (queuePlayer is not null || inMatch)
        {
            await discordContext.TempResponse(queuePlayer is not null ? "You are already in the queue!" : "You are already in a match!");
        }
        else
        {
            await _queryService.AddPlayerToQueue(user.Id);

            _ = RefreshQueueDisplay();
            await discordContext.TempResponse("You have joined the queue! 🎉");
        }
    }

    public async Task LeaveQueue(IDiscordButtonInteractionContext discordContext)
    {
        var user = await UserGuard(discordContext);
        if (user is null) return;

        var queuePlayer = await _queryService.GetQueuePlayer(user.Id);

        if (queuePlayer is null)
        {
            await discordContext.TempResponse("You are not in the matchmaking queue.");
        }
        else
        {
            await _queryService.RemovePlayerToQueue(user.Id);
            _ = RefreshQueueDisplay();
            await discordContext.TempResponse("You have left the queue");
        }
    }

    public async Task VoteForWinner(IDiscordButtonInteractionContext discordContext, int matchId, string winner)
    {
        var user = await UserGuard(discordContext);
        if (user is null) return;

        var parsed = Enum.TryParse<WinnerVote>(winner, out var outValue);
        if (parsed is false)
        {
            await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"I could not parse the value {winner}. Contact developer, expected values are {string.Join(',', Enum.GetValues<WinnerVote>())}",
            }));
            return;
        }

        // The players' gamertags and Discord names, for the kick menu in the vote message
        var match = await _context.MatchedMatches
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User.XboxUser)
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.XboxUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            .Where(x => x.Active)
            .FirstOrDefaultAsync(x => x.Id == matchId);

        if (match is null)
        {
            await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
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
            await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You are not allowed to vote since you are not in this match",
                Flags = MessageFlags.Ephemeral,
            }));
            return;
        }
        else if (me.Kicked)
        {
            await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
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

        await discordContext.TempResponse($"Thanks for voting! You voted for {outValue}");
    }
}
