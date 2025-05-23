﻿using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using System.Text;

namespace GrifballWebApp.Server.Matchmaking;

public class DisplayQueueService : BackgroundService
{
    private readonly ILogger<DisplayQueueService> _logger;
    private readonly SemaphoreSlim _sempaphoreSlim = new(1, 1);
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    private Task? _queueTask;
    public DisplayQueueService(ILogger<DisplayQueueService> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _queueTask = Task.Run(async () =>
        {
            try
            {
                await Go(ct);
            } catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the DisplayQueueService");
            }
            while (await _timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await Go(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing the DisplayQueueService");
                }
            };
        }, ct);
        return Task.CompletedTask;
    }

    public async Task Go(CancellationToken ct)
    {
        await _sempaphoreSlim.WaitAsync(ct);
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var discordOptions = scope.ServiceProvider.GetRequiredService<IOptions<DiscordOptions>>();
            var queueChannel = discordOptions.Value.QueueChannel;
            if (queueChannel is 0)
                throw new Exception("Discord:QueueChannel is not set");

            var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
            var restClient = scope.ServiceProvider.GetRequiredService<IDiscordClient>();
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var me = await restClient.GetCurrentUserAsync(null, ct);

            var messages = (await restClient.GetMessagesAsync(queueChannel, new PaginationProperties<ulong>()
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
                await restClient.DeleteMessagesAsync(queueChannel, cleanup.Select(x => x.Id), null, ct);
            }

            // Start transaction here since we are going to be doing multiple db calls
            var transaction = await context.Database.BeginTransactionAsync(ct);

            var queuePlayers = await queueService.GetQueuePlayersWithInfo(ct);
            var activeMatches = await queueService.GetActiveMatches(ct);

            // Sanity check remove any players from queue that are in active match.
            var activePlayerIds = activeMatches.SelectMany(match => match.HomeTeam.Players.Concat(match.AwayTeam.Players))
                .Select(x => x.DiscordUserID).ToArray();
            if (activePlayerIds.Any())
            {
                var playersToRemove = queuePlayers.Where(x => activePlayerIds.Contains(x.DiscordUserID)).ToArray();
                if (playersToRemove.Any())
                {
                    context.QueuedPlayer.RemoveRange(playersToRemove);
                    await context.SaveChangesAsync(ct);
                }
            }

            var message = filteredMessages.FirstOrDefault();

            if (message is null)
            {
                await restClient.SendMessageAsync(queueChannel, await CreateQueueMessage(queuePlayers, activeMatches, context), null, ct);
            }
            else
            {
                var msg = await CreateQueueMessage(queuePlayers, activeMatches, context);
                await restClient.ModifyMessageAsync(queueChannel, message.Id, (x) =>
                {
                    x.Content = msg.Content;
                    x.Embeds = msg.Embeds;
                    x.Components = msg.Components;
                }, null, ct);
            }

            // Remove timed out players??
            var minPlayersRequired = discordOptions.Value.MatchPlayers;
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
                context.QueuedPlayer.RemoveRange(selectPlayers);

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

                    var team1mmr = team1.Average(x => x.DiscordUser.MMR);
                    var team2mmr = team2.Average(x => x.DiscordUser.MMR);

                    var team1Obj = new MatchedTeam()
                    {
                        Players = team1.Select(x => new MatchedPlayer()
                        {
                            DiscordUserID = x.DiscordUserID,
                        }).ToList(),
                    };
                    var team2Obj = new MatchedTeam()
                    {
                        Players = team2.Select(x => new MatchedPlayer()
                        {
                            DiscordUserID = x.DiscordUserID,
                        }).ToList(),
                    };

                    var matchedMatch = new MatchedMatch()
                    {
                        HomeTeam = team1Obj,
                        AwayTeam = team2Obj,
                    };
                    context.MatchedMatches.Add(matchedMatch);

                    await context.SaveChangesAsync(ct);

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
                    var newMatchMessage = await restClient.SendMessageAsync(discordOptions.Value.LogChannel, mp);
                    var thread = await restClient.CreateGuildThreadAsync(discordOptions.Value.LogChannel, newMatchMessage.Id, new GuildThreadFromMessageProperties($"Match #{matchedMatch.Id}")
                    {
                        Name = $"Match #{matchedMatch.Id}",
                        AutoArchiveDuration = ThreadArchiveDuration.OneHour,
                    }, null, ct);
                    var discordUsers = matchedMatch.HomeTeam.Players.Select(x => x.DiscordUser).Concat(matchedMatch.AwayTeam.Players.Select(x => x.DiscordUser)).ToArray();
                    foreach (var user in discordUsers)
                    {
                        // Going to wrap in try catch. My gut says maybe this could somehow fail is user for whatever reason is not in the guild anymore?
                        try
                        {
                            await thread.AddUserAsync((ulong)user.DiscordUserID, null, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to add user to thread {UserId}", user.DiscordUserID);
                        }
                    }

                    var kickable = matchedMatch.HomeTeam.Players
                        .Select(x => x.DiscordUser)
                        .Union(matchedMatch.AwayTeam.Players.Select(x => x.DiscordUser))
                        .ToArray();
                    MessageProperties voteMessageProperties = CreateVoteMessage(matchedMatch.Id, kickable);
                    var voteMessage = await restClient.SendMessageAsync(thread.Id, voteMessageProperties, null, ct);

                    matchedMatch.ThreadID = thread.Id;
                    matchedMatch.VoteMessageID = voteMessage.Id;
                    await context.SaveChangesAsync();
                }
            }

            // Grab the last 2 matches played for all players
            var dataPuller = scope.ServiceProvider.GetRequiredService<IDataPullService>();
            var allXboxIds = activeMatches.SelectMany(match => match.HomeTeam.Players.Concat(match.AwayTeam.Players))
                .Select(x => x.DiscordUser.XboxUserID)
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .ToList();
            await dataPuller.DownloadRecentMatchesForPlayers(allXboxIds, startPage: 0, endPage: 0, perPage: 2, ct);

            // Now for each match check for any possible match
            foreach (var match in activeMatches)
            {
                var t1 = match.HomeTeam.Players.Where(x => x.Kicked is false).Select(x => x.DiscordUser.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();
                var t2 = match.AwayTeam.Players.Where(x => x.Kicked is false).Select(x => x.DiscordUser.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();

                var matchFound = await context.Matches
                    .Include(x => x.MatchTeams)
                    .ThenInclude(x => x.MatchParticipants)
                        .ThenInclude(x => x.XboxUser)
                            .ThenInclude(x => x.DiscordUser)
                    .Where(x => x.MatchTeams.Count == 2)
                    .Where(x => x.MatchTeams.All(t => t.Outcome == Outcomes.Won || t.Outcome == Outcomes.Lost))
                    .Where(x => x.MatchTeams.Any(t => t.MatchParticipants.Select(p => p.XboxUserID).All(x => t1.Contains(x))) &&
                                x.MatchTeams.Any(t => t.MatchParticipants.Select(p => p.XboxUserID).All(x => t2.Contains(x))))
                    .Where(x => x.StartTime >= match.CreatedAt.AddMinutes(-3)) // the match must have started after the match was created, with 3 minute buffer in case clock is off
                    .Where(x => x.MatchedMatch == null) // the match must not already be matched
                    .OrderByDescending(x => x.StartTime) // Grab the most recent match
                    .FirstOrDefaultAsync(ct);

                var majority = (t1.Count + t2.Count) / 2;
                var voteResult = await context.MatchedWinnerVotes
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
                    var winners = winningTeam.MatchParticipants.Select(x => x.XboxUser.DiscordUser!);
                    var losers = losingTeam.MatchParticipants.Select(x => x.XboxUser.DiscordUser!);
                    var winnerId = winningTeam.TeamID;

                    await HandleWinnersAndLosers(discordOptions, restClient, context, match, matchFound.Duration, winners, losers, winnerId, ct);
                }
                else if (voteResult is { Key: WinnerVote.Home or WinnerVote.Away})
                {
                    // No Match Id since manually ended
                    match.Active = false;
                    var winningTeam = voteResult.Key == WinnerVote.Home ? match.HomeTeam : match.AwayTeam;
                    var losingTeam = voteResult.Key == WinnerVote.Home ? match.AwayTeam : match.HomeTeam;
                    var winners = winningTeam.Players.Select(x => x.DiscordUser);
                    var losers = losingTeam.Players.Select(x => x.DiscordUser);
                    var winnerId = voteResult.Key == WinnerVote.Home ? 0 : 1;
                    await HandleWinnersAndLosers(discordOptions, restClient, context, match, null, winners, losers, winnerId, ct);
                }
                else if (voteResult is { Key: WinnerVote.Cancel } || match.CreatedAt - DateTime.UtcNow > TimeSpan.FromHours(2))
                {
                    // Cancel the match
                    match.Active = false;
                    await context.SaveChangesAsync(ct); // No method calls save changes so do so here

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
                    await restClient.SendMessageAsync(discordOptions.Value.LogChannel, mp);
                    await CloseThread(restClient, match.ThreadID, ct);
                }
            }

            await transaction.CommitAsync(ct);
        }
        finally
        {
            _sempaphoreSlim.Release();
        }
    }

    private async Task HandleWinnersAndLosers(IOptions<DiscordOptions> discordOptions, IDiscordClient restClient, GrifballContext context, MatchedMatch match, TimeSpan? duration, IEnumerable<DiscordUser> winners, IEnumerable<DiscordUser> losers, int winnerId, CancellationToken ct)
    {
        // Adjust player MMR
        var oldMMR = new Dictionary<long, int>();

        var mmrGain = (int)Math.Round(discordOptions.Value.KFactor * 0.75);

        foreach (var player in winners)
        {
            oldMMR.TryAdd(player.DiscordUserID, player.MMR);

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
            oldMMR.TryAdd(player.DiscordUserID, player.MMR);

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

    private async Task CloseThread(IDiscordClient restClient, ulong? threadId, CancellationToken ct)
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

    public static MessageProperties CreateVoteMessage(int matchId, DiscordUser[] kickablePlayers)
    {
        var home = new ButtonProperties("voteforwinner:" + matchId + ":Home", "Home Team Won", ButtonStyle.Primary);
        var away = new ButtonProperties("voteforwinner:" + matchId + ":Away", "Away Team Won", ButtonStyle.Secondary);
        var cancel = new ButtonProperties("voteforwinner:" + matchId + ":Cancel", "Cancel", ButtonStyle.Secondary);

        var kickPlayerMenu = new StringMenuProperties("votetokick:" + matchId)
        {
            Placeholder = "Vote to kick",
            CustomId = "votetokick:" + matchId,
            Options = [.. kickablePlayers.Select(x => new StringMenuSelectOptionProperties(x.DiscordUsername, x.DiscordUserID.ToString()))],
        };

        ActionRowProperties actionRow = new([home, away, cancel]);
        var voteMessageProperties = new MessageProperties()
        {
            Content = $"This match can be now be started in game. Stats will automatically be recorded after the match. If needed, the match can be ended manually by majority vote using one of the options below:",
            Components = [actionRow, kickPlayerMenu],
        };
        return voteMessageProperties;
    }


    public async Task UpdateThreadMessage(MatchedMatch? match, GrifballContext _context, IDiscordClient _discordClient) // TODO: Use DI
    {
        if (match is not null && match.ThreadID is not null)
        {
            var kickable = match.HomeTeam.Players
                .Union(match.AwayTeam.Players)
                .Where(x => x.Kicked is false)
                .Select(x => x.DiscordUser)
                .ToArray();
            var message = CreateVoteMessage(match.Id, kickable);
            var votes = await _context.MatchedWinnerVotes
                .Include(x => x.MatchedPlayer.DiscordUser)
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
                    Description = string.Join(", ", voteGroup.Select(x => x.MatchedPlayer.DiscordUser.DiscordUsername)),
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
            sb.Append(player.DiscordUser.DiscordUsername);
            sb.Append(": ");
            var found = oldMMR.TryGetValue(player.DiscordUserID, out var oldMmr);
            int? OLDMMR = found ? oldMmr : null;
            if (OLDMMR is not null)
                sb.Append(OLDMMR);
            else
                sb.Append("UNK");

            sb.Append(" -> ");

            sb.Append(player.DiscordUser.MMR);

            sb.Append(" (");
            if (OLDMMR is null)
            {
                sb.Append("UNK");
            }
            else
            {
                var change = player.DiscordUser.MMR - OLDMMR.Value;
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
        var join = new ButtonProperties("join_queue", "Join Queue", new EmojiProperties("✅"), ButtonStyle.Success);
        var leave = new ButtonProperties("leave_queue", "Leave Queue", new EmojiProperties("❌"), ButtonStyle.Danger);
        
        ActionRowProperties actionRow = new([join, leave]);

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
                var rank = GetPlayerRank(player.DiscordUser, ranks);
                // TODO: figure out emoji or img?
                if (rank is null)
                    return $"{index + 1}. {player.DiscordUser.DiscordUsername} (MMR: {player.DiscordUser.MMR}) = Queued {waitTime}";
                else 
                    return $"{index + 1}. {player.DiscordUser.DiscordUsername} [{rank.Name}] (MMR: {player.DiscordUser.MMR}) = Queued {waitTime}";
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
            var matchDuration = FormatDuration(match.CreatedAt);
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
            var players = team.Players.Select(player => $"{player.DiscordUser.DiscordUsername} (MMR: {player.DiscordUser.MMR})");
            var str = string.Join("\n", players);
            matchEmbed.AddFields(new EmbedFieldProperties()
            {
                Name = $"Home (Avg MMR: {team.Players.Average(x => x.DiscordUser.MMR)})",
                Value = string.IsNullOrWhiteSpace(str) ? "No players" : str,
                Inline = true,
            });

            team = match.AwayTeam;
            players = team.Players.Select(player => $"{player.DiscordUser.DiscordUsername} (MMR: {player.DiscordUser.MMR})");
            str = string.Join("\n", players);
            matchEmbed.AddFields(new EmbedFieldProperties()
            {
                Name = $"Away (Avg MMR: {team.Players.Average(x => x.DiscordUser.MMR)})",
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

    private Rank? GetPlayerRank(DiscordUser player, Rank[] ranks)
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
