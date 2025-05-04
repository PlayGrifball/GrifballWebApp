using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
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

                    var home = new ButtonProperties("voteforwinner:" + matchedMatch.Id + ":Home", "Home Team Won", ButtonStyle.Primary);
                    var away = new ButtonProperties("voteforwinner:" + matchedMatch.Id + ":Away", "Away Team Won", ButtonStyle.Secondary);
                    var cancel = new ButtonProperties("voteforwinner:" + matchedMatch.Id + ":Cancel", "Cancel", ButtonStyle.Secondary);
                    var foo = new ButtonProperties("voteforwinner:" + matchedMatch.Id + ":dsfds", "Break Me", ButtonStyle.Secondary);

                    ActionRowProperties actionRow = new([home, away, cancel, foo]);
                    await restClient.SendMessageAsync(thread.Id, new()
                    {
                        Content = $"The dough is rising",
                        Components = [actionRow],
                    }, null, ct);

                    matchedMatch.ThreadID = thread.Id;
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
                var t1 = match.HomeTeam.Players.Select(x => x.DiscordUser.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();
                var t2 = match.AwayTeam.Players.Select(x => x.DiscordUser.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();

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

                if (matchFound != null)
                {
                    match.MatchID = matchFound.MatchID;
                    match.Active = false;

                    // Adjust player MMR

                    var oldMMR = new Dictionary<long, int>();

                    var mmrGain = (int)Math.Round(discordOptions.Value.KFactor * 0.75);

                    var winningTeam = matchFound.MatchTeams.FirstOrDefault(x => x.Outcome == Outcomes.Won)
                        ?? throw new Exception("No winning team");
                    foreach (var player in winningTeam.MatchParticipants)
                    {
                        var discordUser = player.XboxUser.DiscordUser
                            ?? throw new Exception("Discord user missing for xbox user");

                        oldMMR.TryAdd(discordUser.DiscordUserID, discordUser.MMR);

                        discordUser.Wins++;
                        discordUser.WinStreak++;
                        discordUser.LossStreak = 0;

                        var streakModifier = 0;
                        if (discordUser.WinStreak >= discordOptions.Value.WinThreshold)
                        {
                            streakModifier = Math.Min(discordOptions.Value.MaxBonus, (discordUser.WinStreak - discordOptions.Value.WinThreshold + 1) * discordOptions.Value.BonusPerWin);
                        }

                        discordUser.MMR += mmrGain + streakModifier;
                    }

                    var mmrLoss = (int)Math.Round(discordOptions.Value.KFactor * 0.625);

                    var losingTeam = matchFound.MatchTeams.FirstOrDefault(x => x.Outcome == Outcomes.Lost)
                        ?? throw new Exception("No losing team");
                    foreach (var player in losingTeam.MatchParticipants)
                    {
                        var discordUser = player.XboxUser.DiscordUser
                            ?? throw new Exception("Discord user missing for xbox user");

                        oldMMR.TryAdd(discordUser.DiscordUserID, discordUser.MMR);

                        discordUser.Losses++;
                        discordUser.WinStreak = 0;
                        discordUser.LossStreak++;

                        var streakModifier = 0;
                        if (discordUser.Losses >= discordOptions.Value.LossThreshold)
                        {
                            streakModifier = Math.Min(discordOptions.Value.MaxPenalty, (discordUser.LossStreak - discordOptions.Value.LossThreshold + 1) * discordOptions.Value.PenaltyPerLoss);
                        }

                        discordUser.MMR -= mmrGain - streakModifier;
                        if (discordUser.MMR < 0)
                        {
                            discordUser.MMR = 0; // Prevent negative MMR
                        }
                    }

                    await context.SaveChangesAsync(ct);

                    var mp = new MessageProperties()
                    {
                        Embeds = new[]
                        {
                            new EmbedProperties()
                            {
                                Title = "Match Ended",
                                Description = $"Match #{match.Id} has been matched. Team {winningTeam.TeamID} is the winner",
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
                                        Value = winningTeam.TeamID.ToString(),
                                        Inline = true,
                                    },
                                    new EmbedFieldProperties()
                                    {
                                        Name = "Duration",
                                        Value = matchFound.Duration.ToString(),
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
                        },
                    };
                    await restClient.SendMessageAsync(discordOptions.Value.LogChannel, mp);

                    // Delete thread soon...
                    if (match.ThreadID is not null)
                    {
                        try
                        {
                            await restClient.SendMessageAsync(match.ThreadID.Value, new() { Content = "Match Completed. This thread will be deleted in about 10 seconds..."});
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(TimeSpan.FromSeconds(10));
                                try
                                {
                                    // Needs permission to manage threads
                                    await restClient.DeleteChannelAsync(match.ThreadID.Value, null, ct);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to delete thread {ThreadId}", match.ThreadID.Value);
                                }
                            }, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send message to thread {ThreadId}", match.ThreadID.Value);
                        }
                    }

                    // TODO: Requeue players?
                }
            }

            await transaction.CommitAsync(ct);
        }
        finally
        {
            _sempaphoreSlim.Release();
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
