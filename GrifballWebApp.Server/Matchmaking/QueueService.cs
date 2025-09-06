using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
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
    public QueueService(ILogger<QueueService> logger,
        IOptions<DiscordOptions> discordOptions,
        IQueueRepository queueRepository,
        IDiscordRestClient discordClient,
        GrifballContext context,
        IDataPullService dataPullService)
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
    }

    public async Task Go(CancellationToken ct)
    {
        await _sempaphoreSlim.WaitAsync(ct);
        try
        {
            // Start transaction here since we are going to be doing multiple db calls
            var transaction = await _context.Database.BeginTransactionAsync(ct);

            var queuePlayers = await _queueRepository.GetQueuePlayersWithInfo(ct);
            var activeMatches = await _queueRepository.GetActiveMatches(ct);

            await RemoveActivePlayersFromQueue(queuePlayers, activeMatches, ct);

            await UpdateQueueMessage(queuePlayers, activeMatches, ct);

            await CreateMatches(queuePlayers, ct);
            await FinishMatches(activeMatches, ct);

            await transaction.CommitAsync(ct);
        }
        finally
        {
            _sempaphoreSlim.Release();
        }
    }

    private async Task FinishMatches(MatchedMatch[] activeMatches, CancellationToken ct)
    {
        // Grab the last 2 matches played for all players
        var allXboxIds = activeMatches.SelectMany(match => match.HomeTeam.Players.Concat(match.AwayTeam.Players))
            .Select(x => x.User.XboxUserID)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();
        await _dataPullService.DownloadRecentMatchesForPlayers(allXboxIds, startPage: 0, endPage: 0, perPage: 2, ct);

        // Now for each match check for any possible match
        foreach (var match in activeMatches)
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

                await HandleWinnersAndLosers(_discordOptions, _discordClient, _context, match, matchFound.Duration, winners, losers, winnerId, ct);
            }
            else if (voteResult is { Key: WinnerVote.Home or WinnerVote.Away })
            {
                // No Match Id since manually ended
                match.Active = false;
                var winningTeam = voteResult.Key == WinnerVote.Home ? match.HomeTeam : match.AwayTeam;
                var losingTeam = voteResult.Key == WinnerVote.Home ? match.AwayTeam : match.HomeTeam;
                var winners = winningTeam.Players.Select(x => x.User);
                var losers = losingTeam.Players.Select(x => x.User);
                var winnerId = voteResult.Key == WinnerVote.Home ? 0 : 1;
                await HandleWinnersAndLosers(_discordOptions, _discordClient, _context, match, null, winners, losers, winnerId, ct);
            }
            else if (voteResult is { Key: WinnerVote.Cancel } || match.StartedAt - DateTime.UtcNow > TimeSpan.FromHours(2))
            {
                // Cancel the match
                match.Active = false;
                await _context.SaveChangesAsync(ct); // No method calls save changes so do so here

                var mp = new MessageProperties()
                {
                    Embeds =
                    [
                        new EmbedProperties()
                            {
                                Title = "Match Canceled",
                                Description = $"Match #{match.Id} has been canceled.",
                                Color = new Color(255, 0, 0), // Red
                                Fields =
                                [
                                    new EmbedFieldProperties()
                                    {
                                        Name = "Match ID",
                                        Value = match.Id.ToString(),
                                        Inline = true,
                                    },
                                ],
                            },
                        ],
                };
                await _discordClient.SendMessageAsync(_discordOptions.Value.LogChannel, mp);
                await CloseThread(_discordClient, match.ThreadID, ct);
            }
        }
    }

    private async Task CreateMatches(QueuedPlayer[] queuePlayers, CancellationToken ct)
    {
        // Remove timed out players??
        var minPlayersRequired = _discordOptions.Value.MatchPlayers;
        //var queueSize = await queueService.GetQueueSize(ct);
        var queueSize = queuePlayers.Length;

        if (queueSize >= minPlayersRequired)
        {
            // Create match
            // emit event to refresh display??

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
                var team1 = new List<QueuedPlayer>();
                var team2 = new List<QueuedPlayer>();

                foreach (var (player, index) in group.Select((p, i) => (p, i)))
                {
                    if (index % 2 == 0)
                    {
                        team1.Add(player);
                    }
                    else
                    {
                        team2.Add(player);
                    }
                }

                var team1mmr = team1.Average(x => x.User.MMR);
                var team2mmr = team2.Average(x => x.User.MMR);

                var team1Obj = new MatchedTeam()
                {
                    Players = team1.Select(x => new MatchedPlayer()
                    {
                        UserID = x.UserID,
                    }).ToList(),
                };
                var team2Obj = new MatchedTeam()
                {
                    Players = team2.Select(x => new MatchedPlayer()
                    {
                        UserID = x.UserID,
                    }).ToList(),
                };

                var matchedMatch = new MatchedMatch()
                {
                    HomeTeam = team1Obj,
                    AwayTeam = team2Obj,
                };
                _context.MatchedMatches.Add(matchedMatch);

                await _context.SaveChangesAsync(ct);

                var matchEmbed = new EmbedProperties()
                {
                    Title = "Match Created",
                    Description = $"Match #{matchedMatch.Id} has been created successfully",
                    Fields =
                    [
                        new EmbedFieldProperties()
                            {
                                Name = "Match ID",
                                Value = matchedMatch.Id.ToString(),
                                Inline = true,
                            },
                            new EmbedFieldProperties()
                            {
                                Name = "Players",
                                Value = (matchedMatch.HomeTeam.Players.Count + matchedMatch.AwayTeam.Players.Count).ToString(),
                                Inline = true,
                            },
                        ],
                };

                var mp = new MessageProperties()
                {
                    Content = $"Match #{matchedMatch.Id} has been created successfully",
                    Embeds = [matchEmbed],
                };
                var newMatchMessage = await _discordClient.SendMessageAsync(_discordOptions.Value.LogChannel, mp);
                var thread = await _discordClient.CreateGuildThreadAsync(_discordOptions.Value.LogChannel, newMatchMessage.Id, new GuildThreadFromMessageProperties($"Match #{matchedMatch.Id}")
                {
                    Name = $"Match #{matchedMatch.Id}",
                    AutoArchiveDuration = ThreadArchiveDuration.OneHour,
                }, null, ct);
                var discordUsers = matchedMatch.HomeTeam.Players.Select(x => x.User.DiscordUser).Concat(matchedMatch.AwayTeam.Players.Select(x => x.User.DiscordUser))
                    .Where(x => x != null)
                    .ToArray();
                foreach (var user in discordUsers)
                {
                    // Going to wrap in try catch. My gut says maybe this could somehow fail is user for whatever reason is not in the guild anymore?
                    try
                    {
                        await thread.AddUserAsync((ulong)user!.DiscordUserID, null, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add user to thread {UserId}", user?.DiscordUserID);
                    }
                }

                var kickable = matchedMatch.HomeTeam.Players
                    .Select(x => x.User)
                    .Union(matchedMatch.AwayTeam.Players.Select(x => x.User))
                    .ToArray();
                MessageProperties voteMessageProperties = CreateVoteMessage(matchedMatch.Id, kickable);
                var voteMessage = await _discordClient.SendMessageAsync(thread.Id, voteMessageProperties, null, ct);

                matchedMatch.ThreadID = thread.Id;
                matchedMatch.VoteMessageID = voteMessage.Id;
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task RemoveActivePlayersFromQueue(QueuedPlayer[] queuePlayers, MatchedMatch[] activeMatches, CancellationToken ct)
    {
        // Sanity check remove any players from queue that are in active match.
        var activePlayerIds = activeMatches.SelectMany(match => match.HomeTeam.Players.Concat(match.AwayTeam.Players))
            .Select(x => x.UserID).ToArray();
        if (activePlayerIds.Any())
        {
            var playersToRemove = queuePlayers.Where(x => activePlayerIds.Contains(x.UserID)).ToArray();
            if (playersToRemove.Any())
            {
                _context.QueuedPlayer.RemoveRange(playersToRemove);
                await _context.SaveChangesAsync(ct);
            }
        }
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

    private async Task HandleWinnersAndLosers(IOptions<DiscordOptions> discordOptions, IDiscordRestClient restClient, GrifballContext context, MatchedMatch match, TimeSpan? duration, IEnumerable<Database.Models.User> winners, IEnumerable<Database.Models.User> losers, int winnerId, CancellationToken ct)
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
            if (player.Losses >= discordOptions.Value.LossThreshold)
            {
                streakModifier = Math.Min(discordOptions.Value.MaxPenalty, (player.LossStreak - discordOptions.Value.LossThreshold + 1) * discordOptions.Value.PenaltyPerLoss);
            }

            player.MMR -= mmrGain - streakModifier;
            if (player.MMR < 0)
            {
                player.MMR = 0; // Prevent negative MMR
            }
        }

        await context.SaveChangesAsync(ct);

        var mp = new MessageProperties()
        {
            Embeds =
            [
                new EmbedProperties()
                {
                    Title = "Match Ended",
                    Description = $"Match #{match.Id} has ended. Team {winnerId} is the winner",
                    Color = new Color(0, 255, 0), // Green
                    Fields =
                    [
                        new EmbedFieldProperties()
                        {
                            Name = "Match ID",
                            Value = match.Id.ToString(),
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Winning Team",
                            Value = winnerId.ToString(),
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Duration",
                            Value = duration?.ToString() ?? "Unknown",
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Home Team MMR Changes",
                            Value = GetMMRChanges(oldMMR, match.HomeTeam),
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Away Team MMR Changes",
                            Value = GetMMRChanges(oldMMR, match.AwayTeam),
                            Inline = true,
                        },
                    ],
                },
            ],
        };
        await restClient.SendMessageAsync(discordOptions.Value.LogChannel, mp);
        await CloseThread(restClient, match.ThreadID, ct);

        // TODO: Requeue players?
    }

    private async Task CloseThread(IDiscordRestClient restClient, ulong? threadId, CancellationToken ct)
    {
        // Delete thread soon...
        if (threadId is not null)
        {
            try
            {
                await restClient.SendMessageAsync(threadId.Value, new() { Content = "Match Completed. This thread will be deleted in about 10 seconds..." });
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    try
                    {
                        // Needs permission to manage threads
                        await restClient.DeleteChannelAsync(threadId.Value, null, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete thread {ThreadId}", threadId.Value);
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message to thread {ThreadId}", threadId.Value);
            }
        }
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
                .Include(x => x.MatchedPlayer.User)
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
        foreach(var (player, index) in team.Players.Select((player, index) => (player, index)))
        {
            sb.Append(player.User.DiscordUser?.DiscordUsername ?? player.User.XboxUser?.Gamertag);
            sb.Append(": ");
            var found = oldMMR.TryGetValue(player.Id, out var oldMmr);
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

            if (index != team.Players.Count - 1)
                sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatDuration(DateTime date)
    {
        return $"<t:{date.ToUnix()}:R>";
    }

    private async Task<MessageProperties> CreateQueueMessage(QueuedPlayer[] queuePlayers, MatchedMatch[] activeMatches, GrifballContext grifballContext)
    {
        //var join = new ButtonProperties("join_queue", "Join Queue", new EmojiProperties(1365830914873098341), ButtonStyle.Success);
        var join = new ButtonProperties(DiscordButtonContants.JoinQueue, "Join Queue", new EmojiProperties("✅"), ButtonStyle.Success);
        var leave = new ButtonProperties(DiscordButtonContants.LeaveQueue, "Leave Queue", new EmojiProperties("❌"), ButtonStyle.Danger);
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
