using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
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
    private readonly QueueMembership _membership;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GrifballContext _context;
    private readonly IDiscordRestClient _discordClient;
    private readonly QueueService _displayQueueService;
    private readonly ILogger<MatchmakingButtonHandler> _logger;

    public MatchmakingButtonHandler(QueueMembership membership, IServiceScopeFactory scopeFactory, GrifballContext context, IDiscordRestClient discordClient, QueueService displayQueueService, ILogger<MatchmakingButtonHandler> logger)
    {
        _membership = membership;
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

    private async Task<Domain.Players.User?> UserGuard(IDiscordButtonInteractionContext discordContext)
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)discordContext.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await discordContext.TempResponse(QueueMessages.NoGamertag);
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


    /// <summary>Joins the queue on <see cref="QueueMembership"/>'s rules, the site's too.</summary>
    public async Task JoinQueue(IDiscordButtonInteractionContext discordContext)
    {
        var (result, _) = await _membership.JoinByDiscord((long)discordContext.User.Id);

        if (result is QueueJoinResult.Joined)
            _ = RefreshQueueDisplay();
        await discordContext.TempResponse(QueueMessages.For(result));
    }

    /// <summary>Leaves the queue on <see cref="QueueMembership"/>'s rules, the site's too.</summary>
    public async Task LeaveQueue(IDiscordButtonInteractionContext discordContext)
    {
        var (result, _) = await _membership.LeaveByDiscord((long)discordContext.User.Id);

        if (result is QueueLeaveResult.Left)
            _ = RefreshQueueDisplay();
        await discordContext.TempResponse(QueueMessages.For(result));
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

        // The whole match, with its players' names for the kick menu in the vote message
        var match = await _context.MatchedMatches
            .WithPlayerNames()
            .FirstOrDefaultAsync(x => x.Id == matchId);

        // The match decides who may vote (MatchedMatch.VoteForWinner); here, only what to answer
        var refusal = match?.VoteForWinner(user.Id, outValue) switch
        {
            null or WinnerVoteResult.MatchOver => "Match does not exist or it is no longer active",
            WinnerVoteResult.NotAPlayer => "You are not allowed to vote since you are not in this match",
            WinnerVoteResult.VoterKicked => "You are not allowed to vote since you have been kicked",
            _ => null,
        };
        if (refusal is not null)
        {
            await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = refusal,
                Flags = MessageFlags.Ephemeral,
            }));
            return;
        }
        await _context.SaveChangesAsync();

        await _displayQueueService.UpdateThreadMessage(match!, _context, _discordClient);

        await discordContext.TempResponse($"Thanks for voting! You voted for {outValue}");
    }
}
