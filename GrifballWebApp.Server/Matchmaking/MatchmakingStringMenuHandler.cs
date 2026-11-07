using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// The matchmaking string menus. <see cref="StringMenuInteractions"/> is the NetCord module; it only wraps its context and calls
/// these, so they take our <see cref="IDiscordStringMenuInteractionContext"/> and tests can pass a substitute.
/// </summary>
public class MatchmakingStringMenuHandler
{
    private readonly GrifballContext _context;
    private readonly IDiscordRestClient _discordClient;
    private readonly QueueService _displayQueueService;
    private readonly IQueueNotifier _notifier;
    public MatchmakingStringMenuHandler(GrifballContext context, IDiscordRestClient discordClient, QueueService displayQueueService, IQueueNotifier notifier)
    {
        _notifier = notifier;
        _context = context;
        _discordClient = discordClient;
        _displayQueueService = displayQueueService;
    }

    private async Task<Database.Models.User?> UserGuard(IDiscordStringMenuInteractionContext discordContext)
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

    public async Task VoteToKick(IDiscordStringMenuInteractionContext discordContext, int matchId)
    {
        var user = await UserGuard(discordContext);
        if (user is null) return;

        var value = discordContext.SelectedValues.Select(long.Parse).First();

        // The whole match, with its players' names for the replies and the vote message
        var match = await _context.MatchedMatches
            .WithPlayerNames()
            .Where(m => m.Id == matchId)
            .FirstOrDefaultAsync();

        if (match is null)
        {
            await discordContext.TempResponse("This match does not exist");
            return;
        }

        // The match decides who may vote and when a majority kicks (MatchedMatch.VoteToKick); here, only what to answer
        var (result, me, them) = match.VoteToKick(user.Id, (int)value);
        var refusal = result switch
        {
            KickVoteResult.MatchOver => "This match is over",
            KickVoteResult.NotAPlayer => "You are not in this match",
            KickVoteResult.VoterKicked => "You have been kicked. You cannot vote.",
            KickVoteResult.TargetNotAPlayer => "They are not in this match",
            KickVoteResult.TargetAlreadyKicked => $"{them!.ToDisplayName()} has already been kicked",
            KickVoteResult.AlreadyVoted => $"You have already voted to kick {them!.ToDisplayName()}",
            _ => null,
        };
        if (refusal is not null)
        {
            await discordContext.TempResponse(refusal);
            return;
        }
        await _context.SaveChangesAsync();

        if (result is KickVoteResult.Kicked)
        {
            // Out of the match: their home page offers the queue again
            _ = _notifier.SendStatusChanged([them!.UserID]);
            await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"{them.ToDisplayName()} has been kicked",
            }));
            await _displayQueueService.UpdateThreadMessage(match, _context, _discordClient);
            return;
        }

        await _displayQueueService.UpdateThreadMessage(match, _context, _discordClient);

        await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = $"{me!.ToDisplayName()} has voted to kick {them!.ToDisplayName()}",
        }));
    }
}
