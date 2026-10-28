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
    public MatchmakingStringMenuHandler(GrifballContext context, IDiscordRestClient discordClient, QueueService displayQueueService)
    {
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

        var match = await _context.MatchedMatches
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User)
            .Where(m => m.Id == matchId)
            .FirstOrDefaultAsync();

        if (match is null)
        {
            await discordContext.TempResponse("This match does not exist");
            return;
        }
        else if (match.Active is false)
        {
            await discordContext.TempResponse("This match is over");
            return;
        }

        var all = match.HomeTeam.Players
                .Union(match.AwayTeam.Players)
                .ToArray();

        var me = all.FirstOrDefault(x => x.UserID == user.Id);
        var them = all.FirstOrDefault(x => x.UserID == value);

        if (me is null)
        {
            await discordContext.TempResponse("You are not in this match");
            return;
        }
        else if (me.Kicked)
        {
            await discordContext.TempResponse("You have been kicked. You cannot vote.");
            return;
        }
        else if (them is null)
        {
            await discordContext.TempResponse("They are not in this match");
            return;
        }
        else if (them.Kicked)
        {
            await discordContext.TempResponse($"{them.ToDisplayName()} has already been kicked");
            return;
        }

        var vote = await _context.MatchedKickVotes
            .Where(x => x.MatchId == matchId)
            .Where(x => x.VoterMatchedPlayerId == me.Id)
            .Where(x => x.KickMatchedPlayerId == them.Id)
            .FirstOrDefaultAsync();

        if (vote is null)
        {
            vote = new MatchedKickVote
            {
                MatchId = matchId,
                VoterMatchedPlayerId = me.Id,
                KickMatchedPlayerId = them.Id,
            };
            _context.MatchedKickVotes.Add(vote);
            await _context.SaveChangesAsync();

            var playerCount = await _context.MatchedPlayers
                .Where(x => x.MatchedTeam!.HomeMatchedMatch!.Id == matchId || x.MatchedTeam!.AwayMatchedMatch!.Id == matchId)
                .Where(x => x.Kicked == false)
                .CountAsync();

            var kickVote = await _context.MatchedKickVotes
                .Where(x => x.VoterMatchedPlayer.Kicked == false) // Dont count kicked players
                .Where(x => x.MatchId == matchId)
                .Where(x => x.KickMatchedPlayerId == them.Id)
                .CountAsync();

            var majority = playerCount / 2;
            var thresholdReached = kickVote > majority;

            if (thresholdReached)
            {
                var player = all.FirstOrDefault(x => x.UserID == value);
                if (player is not null)
                {
                    player.Kicked = true;
                    await _context.SaveChangesAsync();

                    await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
                    {
                        Content = $"{them.ToDisplayName()} has been kicked",
                    }));
                    await _displayQueueService.UpdateThreadMessage(match, _context, _discordClient);
                    return;
                }
                else
                {
                    // log error, return error response
                }
            }
        }
        else
        {
            await discordContext.TempResponse($"You have already voted to kick {them.ToDisplayName()}");
            return;
        }

        await _displayQueueService.UpdateThreadMessage(match, _context, _discordClient);

        await discordContext.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = $"{me.ToDisplayName()} has voted to kick {them.ToDisplayName()}",
        }));
    }
}
