using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

public class StringMenuInteractions : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly GrifballContext _context;
    private readonly IDiscordClient _discordClient;
    private readonly QueueService _displayQueueService;
    public StringMenuInteractions(GrifballContext context, IDiscordClient discordClient, QueueService displayQueueService)
    {
        _context = context;
        _discordClient = discordClient;
        _displayQueueService = displayQueueService;
    }

    [ComponentInteraction("votetokick")]
    public async Task VoteToKick(int matchId)
    {
        var value = Context.SelectedValues.Select(long.Parse).First();

        var match = await _context.MatchedMatches
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.DiscordUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.DiscordUser)
            .Where(m => m.Id == matchId)
            .FirstOrDefaultAsync();

        if (match is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"This match does not exist",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }
        else if (match.Active is false)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"This match is over",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        var all = match.HomeTeam.Players
                .Union(match.AwayTeam.Players)
                .ToArray();

        var me = all.FirstOrDefault(x => x.DiscordUserID == (long)Context.User.Id);
        var them = all.FirstOrDefault(x => x.DiscordUserID == value);

        if (me is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You are not in this match",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }
        else if (me.Kicked)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You have been kicked. You cannot vote.",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }
        else if (them is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"They are not in this match",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }
        else if (them.Kicked)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"{them.DiscordUser.DiscordUsername} has already been kicked",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
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
                var player = all.FirstOrDefault(x => x.DiscordUserID == value);
                if (player is not null)
                {
                    player.Kicked = true;
                    await _context.SaveChangesAsync();

                    await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
                    {
                        Content = $"{them.DiscordUser.DiscordUsername} has been kicked",
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
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = $"You have already voted to kick {them.DiscordUser.DiscordUsername}",
                Flags = MessageFlags.Ephemeral,
            }));
            await Task.Delay(5000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        await _displayQueueService.UpdateThreadMessage(match, _context, _discordClient);

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = $"{me.DiscordUser.DiscordUsername} has voted to kick {them.DiscordUser.DiscordUsername}",
        }));
    }
}
