using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using System.Text;

namespace GrifballWebApp.Server.Matchmaking;

public class QueueService
{
    private readonly ILogger<QueueService> _logger;
    private static SemaphoreSlim _sempaphoreSlim = new(1, 1);
    private readonly IOptions<DiscordOptions> _discordOptions;
    private readonly ulong _queueChannel;
    private readonly IQueueRepository _queueRepository;
    private readonly IDiscordRestClient _discordClient;
    private readonly GrifballContext _context;
    private readonly IDataPullService _dataPullService;
    private readonly IOutbox _outbox;
    public QueueService(ILogger<QueueService> logger,
        IOptions<DiscordOptions> discordOptions,
        IQueueRepository queueRepository,
        IDiscordRestClient discordClient,
        GrifballContext context,
        IDataPullService dataPullService,
        IOutbox outbox)
    {
        _logger = logger;
        _discordOptions = discordOptions;
        _queueChannel = discordOptions.Value.QueueChannel;
        if (_queueChannel is 0)
            throw new Exception("Discord:QueueChannel is not set");
        _queueRepository = queueRepository;
        _discordClient = discordClient;
        _context = context;
        _dataPullService = dataPullService;
        _outbox = outbox;
    }

    public async Task Go(CancellationToken ct)
    {
        await _sempaphoreSlim.WaitAsync(ct);
        try
        {
            QueuedPlayer[] queuePlayers;
            MatchedMatch[] activeMatches;
            List<MatchedMatch> createdMatches;

            // The queue clean up, the new matches and their announcements (queued on the outbox, which sends them to
            // Discord once committed) are committed together: a failure rolls all of them back.
            await using (var transaction = await _context.Database.BeginTransactionAsync(ct))
            {
                queuePlayers = await _queueRepository.GetQueuePlayersWithInfo(ct);
                activeMatches = await _queueRepository.GetActiveMatches(ct);

                queuePlayers = await RemoveActivePlayersFromQueue(queuePlayers, activeMatches, ct);

                createdMatches = await CreateMatches(queuePlayers, ct);
                foreach (var match in createdMatches)
                    await _outbox.EnqueueAnnouncementAsync(match.Id, mayBePosted: false, ct);

                await AnnounceMatchesWithoutAnnouncement(activeMatches, ct);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }

            var matchedUserIds = createdMatches.SelectMany(PlayersOf).Select(x => x.UserID).ToHashSet();
            var stillQueued = queuePlayers.Where(x => matchedUserIds.Contains(x.UserID) is false).ToArray();

            // Not through the outbox: the message is rebuilt from the database on every tick, so a failed update is
            // made good by the next one.
            try
            {
                await UpdateQueueMessage(stillQueued, activeMatches, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || ct.IsCancellationRequested is false)
            {
                _logger.LogError(ex, "Failed to update the matchmaking queue message");
            }

            // A match is announced once it has its vote message: until then its players can't vote.
            await FinishMatches(activeMatches.Where(x => x.Active && x.VoteMessageID is not null).ToArray(), ct);
        }
        finally
        {
            _sempaphoreSlim.Release();
        }
    }

    private static IEnumerable<MatchedPlayer> PlayersOf(MatchedMatch match) => match.HomeTeam.Players.Concat(match.AwayTeam.Players);

    private void DiscardPendingChanges() => _context.DiscardPendingChanges();

    /// <summary>
    /// Active matches not announced yet with no announcement in the outbox: made before it, by a version of the app
    /// that announced them straight away and failed to. Their announcement is queued, looking for what was posted.
    /// </summary>
    private async Task AnnounceMatchesWithoutAnnouncement(MatchedMatch[] activeMatches, CancellationToken ct)
    {
        var unannounced = activeMatches.Where(x => x.VoteMessageID is null).ToList();
        if (unannounced.Count == 0)
            return;

        var groups = unannounced.Select(x => MatchOutbox.Group(x.Id)).ToList();
        var queued = (await _context.Outbox.Where(x => x.GroupKey != null && groups.Contains(x.GroupKey)).Select(x => x.GroupKey!).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var match in unannounced.Where(x => queued.Contains(MatchOutbox.Group(x.Id)) is false))
        {
            _logger.LogWarning("Match {MatchId} has no announcement queued; queuing it", match.Id);
            await _outbox.EnqueueAnnouncementAsync(match.Id, mayBePosted: true, ct);
        }
    }

    private async Task FinishMatches(MatchedMatch[] activeMatches, CancellationToken ct)
    {
        // Grab the last 2 matches played for all players
        var allXboxIds = activeMatches.SelectMany(PlayersOf)
            .Select(x => x.User.XboxUserID)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();
        try
        {
            await _dataPullService.DownloadRecentMatchesForPlayers(allXboxIds, startPage: 0, endPage: 0, perPage: 2, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || ct.IsCancellationRequested is false)
        {
            // Halo's API being down must not stop vote endings and cancellations: they need nothing from it.
            _logger.LogError(ex, "Failed to download recent matches for the active matchmaking matches");
            DiscardPendingChanges();
        }

        foreach (var match in activeMatches)
        {
            try
            {
                await FinishMatch(match, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || ct.IsCancellationRequested is false)
            {
                // One match failing to end must not stop the others.
                _logger.LogError(ex, "Failed to finish match {MatchId}", match.Id);
                DiscardPendingChanges();
            }
        }
    }

    private async Task FinishMatch(MatchedMatch match, CancellationToken ct)
    {
        var t1 = match.HomeTeam.Players.Where(x => x.Kicked is false).Select(x => x.User.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();
        var t2 = match.AwayTeam.Players.Where(x => x.Kicked is false).Select(x => x.User.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();

        var matchFound = await _context.Matches
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    //.ThenInclude(x => x.XboxUser.User!.XboxUser) // Do not include because EF will include for us?
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser.User!.DiscordUser)
            .Where(x => x.MatchTeams.Count == 2)
            .Where(x => x.MatchTeams.All(t => t.Outcome == Outcomes.Won || t.Outcome == Outcomes.Lost))
            .Where(x => x.MatchTeams.Any(t => t.MatchParticipants.Select(p => p.XboxUserID).All(x => t1.Contains(x))) &&
                        x.MatchTeams.Any(t => t.MatchParticipants.Select(p => p.XboxUserID).All(x => t2.Contains(x))))
            .Where(x => x.StartTime >= match.StartedAt.AddMinutes(-3)) // the match must have started after the match was created, with 3 minute buffer in case clock is off
            .Where(x => x.MatchedMatch == null) // the match must not already be matched
            .OrderByDescending(x => x.StartTime) // Grab the most recent match
            .FirstOrDefaultAsync(ct);

        var majority = (t1.Count + t2.Count) / 2;
        var voteResult = await _context.MatchedWinnerVotes
            .Where(x => x.MatchId == match.Id)
            .Where(x => x.MatchedPlayer.Kicked == false)
            .GroupBy(x => x.WinnerVote)
            .Select(x => new { x.Key, Count = x.Count() })
            .Where(x => x.Count > majority)
            .FirstOrDefaultAsync();

        if (matchFound != null)
        {
            match.MatchID = matchFound.MatchID;
            match.Active = false;
            var winningTeam = matchFound.MatchTeams.FirstOrDefault(x => x.Outcome == Outcomes.Won)
                ?? throw new Exception("No winning team");
            var losingTeam = matchFound.MatchTeams.FirstOrDefault(x => x.Outcome == Outcomes.Lost)
                ?? throw new Exception("No losing team");
            var winners = winningTeam.MatchParticipants.Select(x => x.XboxUser.User!);
            var losers = losingTeam.MatchParticipants.Select(x => x.XboxUser.User!);
            var winnerId = winningTeam.TeamID;

            await HandleWinnersAndLosers(_discordOptions, _context, match, matchFound.Duration, winners, losers, winnerId, ct);
        }
        else if (voteResult is { Key: WinnerVote.Home or WinnerVote.Away })
        {
            // No Match Id since manually ended
            match.Active = false;
            var winningTeam = voteResult.Key == WinnerVote.Home ? match.HomeTeam : match.AwayTeam;
            var losingTeam = voteResult.Key == WinnerVote.Home ? match.AwayTeam : match.HomeTeam;
            // Kicked players didn't play the match out: they neither win nor lose it.
            var winners = winningTeam.Players.Where(x => x.Kicked is false).Select(x => x.User);
            var losers = losingTeam.Players.Where(x => x.Kicked is false).Select(x => x.User);
            var winnerId = voteResult.Key == WinnerVote.Home ? 0 : 1;
            await HandleWinnersAndLosers(_discordOptions, _context, match, null, winners, losers, winnerId, ct);
        }
        else if (voteResult is { Key: WinnerVote.Cancel } || DateTime.UtcNow - match.StartedAt > TimeSpan.FromHours(2))
        {
            // Cancel the match: its post and thread close are queued with it.
            match.Active = false;
            await _outbox.EnqueueCanceledAsync(match.Id, unannounced: false, ct);
            await _context.SaveChangesAsync(ct); // No method calls save changes so do so here
        }
    }

    /// <summary>Makes the matches and takes their players off the queue. Only the database: their announcements are queued on the outbox.</summary>
    private async Task<List<MatchedMatch>> CreateMatches(QueuedPlayer[] queuePlayers, CancellationToken ct)
    {
        var created = new List<MatchedMatch>();
        var minPlayersRequired = _discordOptions.Value.MatchPlayers;
        var queueSize = queuePlayers.Length;

        if (minPlayersRequired <= 0 || queueSize < minPlayersRequired)
            return created;

        var take = (queueSize / minPlayersRequired) * minPlayersRequired;

        var selectPlayers = queuePlayers
            .OrderBy(x => x.JoinedAt) // Longest queued get to play first
            .Take(take)
            .ToArray();

        var groupedPlayers = selectPlayers
            .Select((player, index) => new { player, groupIndex = index / minPlayersRequired })
            .GroupBy(x => x.groupIndex)
            .Select(group => group.Select(x => x.player).ToArray())
            .ToArray();

        // Remove from queue
        _context.QueuedPlayer.RemoveRange(selectPlayers);

        // Create match for each group
        foreach (var group in groupedPlayers)
        {
            var team1 = group.Where((_, index) => index % 2 == 0).ToList();
            var team2 = group.Where((_, index) => index % 2 == 1).ToList();

            var matchedMatch = new MatchedMatch()
            {
                HomeTeam = new MatchedTeam() { Players = team1.Select(x => new MatchedPlayer() { UserID = x.UserID }).ToList() },
                AwayTeam = new MatchedTeam() { Players = team2.Select(x => new MatchedPlayer() { UserID = x.UserID }).ToList() },
            };
            _context.MatchedMatches.Add(matchedMatch);
            created.Add(matchedMatch);
        }

        await _context.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>
    /// Takes the players who are in an active match off the queue, and returns the rest. A kicked player is no longer in
    /// the match, as <see cref="IQueueRepository.IsInMatch"/> has it, so may queue again.
    /// </summary>
    private async Task<QueuedPlayer[]> RemoveActivePlayersFromQueue(QueuedPlayer[] queuePlayers, MatchedMatch[] activeMatches, CancellationToken ct)
    {
        var activePlayerIds = activeMatches.SelectMany(PlayersOf)
            .Where(x => x.Kicked is false)
            .Select(x => x.UserID)
            .ToHashSet();
        var playersToRemove = queuePlayers.Where(x => activePlayerIds.Contains(x.UserID)).ToArray();
        if (playersToRemove.Length == 0)
            return queuePlayers;

        _context.QueuedPlayer.RemoveRange(playersToRemove);
        await _context.SaveChangesAsync(ct);
        return queuePlayers.Except(playersToRemove).ToArray();
    }

    private async Task UpdateQueueMessage(QueuedPlayer[] queuePlayers, MatchedMatch[] activeMatches, CancellationToken ct)
    {
        var me = await _discordClient.GetCurrentUserAsync(null, ct);

        var messages = (await _discordClient.GetMessagesAsync(_queueChannel, new PaginationProperties<ulong>()
        {
            BatchSize = 20,
        }).Take(20).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToArray();

        var filteredMessages = messages
            .Where(x => x.Author.Id == me.Id && x.Embeds.Any(embed => embed.Title is "Matchmaking Queue"))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        if (filteredMessages.Count > 1)
        {
            var cleanup = filteredMessages.Skip(1).ToList();
            await _discordClient.DeleteMessagesAsync(_queueChannel, cleanup.Select(x => x.Id), null, ct);
        }

        var message = filteredMessages.FirstOrDefault();

        if (message is null)
        {
            await _discordClient.SendMessageAsync(_queueChannel, await CreateQueueMessage(queuePlayers, activeMatches, _context), null, ct);
        }
        else
        {
            var msg = await CreateQueueMessage(queuePlayers, activeMatches, _context);
            await _discordClient.ModifyMessageAsync(_queueChannel, message.Id, (x) =>
            {
                x.Content = msg.Content;
                x.Embeds = msg.Embeds;
                x.Components = msg.Components;
            }, null, ct);
        }
    }

    private async Task HandleWinnersAndLosers(IOptions<DiscordOptions> discordOptions, GrifballContext context, MatchedMatch match, TimeSpan? duration, IEnumerable<Database.Models.User> winners, IEnumerable<Database.Models.User> losers, int winnerId, CancellationToken ct)
    {
        // Adjust player MMR
        var oldMMR = new Dictionary<long, int>();

        var mmrGain = (int)Math.Round(discordOptions.Value.KFactor * 0.75);

        foreach (var player in winners)
        {
            oldMMR.TryAdd(player.Id, player.MMR);

            player.Wins++;
            player.WinStreak++;
            player.LossStreak = 0;

            var streakModifier = 0;
            if (player.WinStreak >= discordOptions.Value.WinThreshold)
            {
                streakModifier = Math.Min(discordOptions.Value.MaxBonus, (player.WinStreak - discordOptions.Value.WinThreshold + 1) * discordOptions.Value.BonusPerWin);
            }

            player.MMR += mmrGain + streakModifier;
        }

        var mmrLoss = (int)Math.Round(discordOptions.Value.KFactor * 0.625);


        foreach (var player in losers)
        {
            oldMMR.TryAdd(player.Id, player.MMR);

            player.Losses++;
            player.WinStreak = 0;
            player.LossStreak++;

            var streakModifier = 0;
            if (player.LossStreak >= discordOptions.Value.LossThreshold)
            {
                streakModifier = Math.Min(discordOptions.Value.MaxPenalty, (player.LossStreak - discordOptions.Value.LossThreshold + 1) * discordOptions.Value.PenaltyPerLoss);
            }

            player.MMR -= mmrLoss + streakModifier;
            if (player.MMR < 0)
            {
                player.MMR = 0; // Prevent negative MMR
            }
        }

        // The end post is worked out now (the MMR changes can't be later) and queued, with its thread close, in the same
        // save as the result.
        await _outbox.EnqueueEndedAsync(new MatchEndedPayload(match.Id, winnerId, duration?.ToString() ?? "Unknown",
            GetMMRChanges(oldMMR, match.HomeTeam), GetMMRChanges(oldMMR, match.AwayTeam)), ct);
        await context.SaveChangesAsync(ct);

        // TODO: Requeue players?
    }

    public static MessageProperties CreateVoteMessage(int matchId, Database.Models.User[] kickablePlayers)
    {
        var home = new ButtonProperties(DiscordButtonContants.VoteForWinnerWithParams(matchId, WinnerVote.Home), "Home Team Won", ButtonStyle.Primary);
        var away = new ButtonProperties(DiscordButtonContants.VoteForWinnerWithParams(matchId, WinnerVote.Away), "Away Team Won", ButtonStyle.Secondary);
        var cancel = new ButtonProperties(DiscordButtonContants.VoteForWinnerWithParams(matchId, WinnerVote.Cancel), "Cancel", ButtonStyle.Secondary);

        var kickPlayerMenu = new StringMenuProperties(DiscordStringMenuContants.VoteToKickWithParams(matchId))
        {
            Placeholder = "Vote to kick",
            CustomId = DiscordStringMenuContants.VoteToKickWithParams(matchId),
            Options = [.. kickablePlayers.Select(x => new StringMenuSelectOptionProperties(x.XboxUser?.Gamertag ?? x.DiscordUser?.DiscordUsername ?? x.DisplayName ?? x.Id.ToString(), x.Id.ToString()))],
        };

        ActionRowProperties actionRow = new([home, away, cancel]);
        var voteMessageProperties = new MessageProperties()
        {
            Content = $"This match can be now be started in game. Stats will automatically be recorded after the match. If needed, the match can be ended manually by majority vote using one of the options below:",
            Components = [actionRow, kickPlayerMenu],
        };
        return voteMessageProperties;
    }


    public async Task UpdateThreadMessage(MatchedMatch? match, GrifballContext _context, IDiscordRestClient _discordClient) // TODO: Use DI
    {
        if (match is not null && match.ThreadID is not null)
        {
            var kickable = match.HomeTeam.Players
                .Union(match.AwayTeam.Players)
                .Where(x => x.Kicked is false)
                .Select(x => x.User)
                .ToArray();
            var message = CreateVoteMessage(match.Id, kickable);
            var votes = await _context.MatchedWinnerVotes
                .Include(x => x.MatchedPlayer.User.XboxUser)
                .Include(x => x.MatchedPlayer.User.DiscordUser)
                .Where(x => x.MatchId == match.Id)
                .Where(x => x.MatchedPlayer.Kicked == false)
                .GroupBy(x => x.WinnerVote)
                .AsNoTracking()
                .ToListAsync();
            if (votes.Any())
            {
                var playerCount = await _context.MatchedPlayers
                    .Where(x => x.MatchedTeam!.HomeMatchedMatch!.Id == match.Id || x.MatchedTeam!.AwayMatchedMatch!.Id == match.Id)
                    .Where(x => x.Kicked == false)
                    .CountAsync();
                var majority = (playerCount / 2) + 1;
                message.AddEmbeds([new()
                {
                    Title = "Votes",
                    Description = $"You need a majority of at least {majority} votes",
                }]);
            }
            foreach (var voteGroup in votes)
            {
                var count = voteGroup.Count();
                message.AddEmbeds([new()
                    {
                    Title = $"{voteGroup.Key} ({count})",
                    Description = string.Join(", ", voteGroup.Select(x => x.MatchedPlayer.User.XboxUser?.Gamertag ?? x.MatchedPlayer.User.DiscordUser?.DiscordUsername ?? x.MatchedPlayer.User.Id.ToString())),
                    }]);
            }
            await _discordClient.ModifyMessageAsync(match.ThreadID.Value, match.VoteMessageID!.Value, x =>
            {
                x.Content = message.Content;
                x.Components = message.Components;
                x.Embeds = message.Embeds ?? [];
            });
        }
    }

    private string GetMMRChanges(Dictionary<long, int> oldMMR, MatchedTeam team)
    {
        var sb = new StringBuilder();
        var players = team.Players.Where(x => x.Kicked is false).ToList(); // Kicked players' MMR is not changed
        foreach(var (player, index) in players.Select((player, index) => (player, index)))
        {
            sb.Append(player.User.DiscordUser?.DiscordUsername ?? player.User.XboxUser?.Gamertag);
            sb.Append(": ");
            var found = oldMMR.TryGetValue(player.UserID, out var oldMmr);
            int? OLDMMR = found ? oldMmr : null;
            if (OLDMMR is not null)
                sb.Append(OLDMMR);
            else
                sb.Append("UNK");

            sb.Append(" -> ");

            sb.Append(player.User.MMR);

            sb.Append(" (");
            if (OLDMMR is null)
            {
                sb.Append("UNK");
            }
            else
            {
                var change = player.User.MMR - OLDMMR.Value;
                if (change > 0)
                    sb.Append("+");
                sb.Append(change);
            }
            sb.Append(")");

            if (index != players.Count - 1)
                sb.AppendLine();
        }
        return sb.Length == 0 ? "No players" : sb.ToString(); // An empty embed field value is rejected by Discord
    }

    private static string FormatDuration(DateTime date)
    {
        return $"<t:{date.ToUnix()}:R>";
    }

    private async Task<MessageProperties> CreateQueueMessage(QueuedPlayer[] queuePlayers, MatchedMatch[] activeMatches, GrifballContext grifballContext)
    {
        //var join = new ButtonProperties("join_queue", "Join Queue", new EmojiProperties(1365830914873098341), ButtonStyle.Success);
        var join = new ButtonProperties(DiscordButtonContants.JoinQueue, "Join Queue", EmojiProperties.Standard("✅"), ButtonStyle.Success);
        var leave = new ButtonProperties(DiscordButtonContants.LeaveQueue, "Leave Queue", EmojiProperties.Standard("❌"), ButtonStyle.Danger);
        var gt = new ButtonProperties(DiscordButtonContants.SetGamertag, "Set Gamertag", ButtonStyle.Primary);

        ActionRowProperties actionRow = new([join, leave, gt]);

        var queueEmbed = new EmbedProperties()
        {
            Title = "Matchmaking Queue",
            Description = $"{queuePlayers.Count()} players in queue",
            Color = new Color(88, 101, 242), // Blue
        };

        if (queuePlayers.Any())
        {
            var ranks = await grifballContext.Ranks
                .OrderBy(x => x.MmrThreshold)
                .ToArrayAsync();

            var map = queuePlayers.Select((player, index) =>
            {
                var waitTime = FormatDuration(player.JoinedAt);
                var rank = GetPlayerRank(player.User, ranks);
                // TODO: figure out emoji or img?
                if (rank is null)
                    return $"{index + 1}. {player.ToDisplayName()} (MMR: {player.User.MMR}) = Queued {waitTime}";
                else 
                    return $"{index + 1}. {player.ToDisplayName()} [{rank.Name}] (MMR: {player.User.MMR}) = Queued {waitTime}";
            });

            queueEmbed.AddFields(new EmbedFieldProperties()
            {
                Name = "Players in Queue",
                Value = string.Join("\n", map),
            });
        }

        var matchEmbeds = new List<EmbedProperties>();
        
        if (activeMatches.Any())
        {
            var matchesSummaryEmbed = new EmbedProperties()
            {
                Color = new Color(87, 242, 135), // green
                Title = "Active Matches",
                Description = $"{activeMatches.Count()} active matches",
            };
            matchEmbeds.Add(matchesSummaryEmbed);
        }
        

        foreach(var match in activeMatches)
        {
            var matchDuration = FormatDuration(match.StartedAt);
            var matchEmbed = new EmbedProperties()
            {
                Color = new Color(59, 165, 92), // orange
                Title = $"Match #{match.Id}",
                Description = $"Duration: {matchDuration}",
                Footer = new EmbedFooterProperties()
                {
                    Text = $"Use /endmatch {match.Id} to Eagle|Cobra to end this match",
                },
            };

            var team = match.HomeTeam;
            var players = team.Players.Select(player => $"{player.ToDisplayName()} (MMR: {player.User.MMR})");
            var str = string.Join("\n", players);
            matchEmbed.AddFields(new EmbedFieldProperties()
            {
                Name = $"Home (Avg MMR: {team.Players.Average(x => x.User.MMR)})",
                Value = string.IsNullOrWhiteSpace(str) ? "No players" : str,
                Inline = true,
            });

            team = match.AwayTeam;
            players = team.Players.Select(player => $"{player.ToDisplayName()} (MMR: {player.User.MMR})");
            str = string.Join("\n", players);
            matchEmbed.AddFields(new EmbedFieldProperties()
            {
                Name = $"Away (Avg MMR: {team.Players.Average(x => x.User.MMR)})",
                Value = string.IsNullOrWhiteSpace(str) ? "No players" : str,
                Inline = true,
            });

            matchEmbeds.Add(matchEmbed);
        }

        return new MessageProperties()
        {
            Content = "Matchmaking Queue",
            Embeds = matchEmbeds.Prepend(queueEmbed).ToArray(),
            Components = [actionRow],
        };
    }

    private Rank? GetPlayerRank(Database.Models.User player, Rank[] ranks)
    {
        if (ranks.Any() is false)
            return null;
        // Find the highest rank that the player qualifies for based on their MMR
        var ordered = ranks.OrderByDescending(x => x.MmrThreshold);
        return ordered.FirstOrDefault(rank => player.MMR >= rank.MmrThreshold) ?? ordered.LastOrDefault()
            ?? throw new Exception("There are no ranks");
    }

    // We must refresh queue display when queue event is update, match event is updated, or match event is ended
    // currently have it hooked to player joining / leaving the queue...
}
