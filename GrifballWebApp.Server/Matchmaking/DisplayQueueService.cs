using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;

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
                Limit = 20,
            }).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToArray();

            var filteredMessages = messages
                .Where(x => x.Author.Id == me.Id && x.Embeds.Any(embed => embed.Title is "Matchmaking Queue"))
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            if (filteredMessages.Count > 1)
            {
                var cleanup = filteredMessages.Skip(1).ToList();
                await restClient.DeleteMessagesAsync(queueChannel, cleanup.Select(x => x.Id), null, ct);
            }

            var queuePlayers = await queueService.GetQueuePlayersWithInfo(ct);
            var activeMatches = await queueService.GetActiveMatches(ct);

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

            var transaction = await context.Database.BeginTransactionAsync(ct);

            // Remove timed out players??
            var minPlayersRequired = 2;
            //var queueSize = await queueService.GetQueueSize(ct);
            var queueSize = queuePlayers.Length;

            if (queueSize < minPlayersRequired)
                return;

            // Create match
            // emit event to refresh display??

            var selectPlayers = queuePlayers
                .OrderBy(x => x.JoinedAt) // Longest queued get to play first
                .Take(minPlayersRequired)
                .ToArray();

            // Remove from queue
            context.QueuedPlayer.RemoveRange(selectPlayers);

            var team1 = new List<QueuedPlayer>();
            var team2 = new List<QueuedPlayer>();

            foreach(var (player, index) in selectPlayers.Select((p, i) => (p, i)))
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

            context.MatchedMatchs.Add(new MatchedMatch()
            {
                HomeTeam = team1Obj,
                AwayTeam = team2Obj,
            });

            await context.SaveChangesAsync(ct);

            // Grab the last 2 matches played for all players
            var dataPuller = scope.ServiceProvider.GetRequiredService<DataPullService>();
            var allXboxIds = activeMatches.SelectMany(match => match.HomeTeam.Players.Concat(match.AwayTeam.Players))
                .Select(x => x.DiscordUser.XboxUserID)
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .ToList();
            await dataPuller.DownloadRecentMatchesForPlayers(allXboxIds, startPage: 0, endPage: 0, perPage: 2, ct);

            // Now for each match check for any possible match
            foreach(var match in activeMatches)
            {
                var t1 = match.HomeTeam.Players.Select(x => x.DiscordUser.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();
                var t2 = match.AwayTeam.Players.Select(x => x.DiscordUser.XboxUserID).Where(x => x is not null).Select(x => x!.Value).ToList();

                // TODO: does this work?
                var matchFound = await context.Matches
                    .Include(x => x.MatchTeams)
                    .ThenInclude(x => x.MatchParticipants)
                        .ThenInclude(x => x.XboxUser)
                            .ThenInclude(x => x.User)
                    .Where(x => x.MatchTeams.Count == 2)
                    .Where(x => (x.MatchTeams.ElementAt(0).MatchParticipants.Select(x => x.XboxUserID).ToList() == t1 &&
                                 x.MatchTeams.ElementAt(1).MatchParticipants.Select(x => x.XboxUserID).ToList() == t2) ||
                                (x.MatchTeams.ElementAt(0).MatchParticipants.Select(x => x.XboxUserID).ToList() == t2 &&
                                 x.MatchTeams.ElementAt(1).MatchParticipants.Select(x => x.XboxUserID).ToList() == t1))
                    .Where(x => x.StartTime >= match.CreatedAt) // the match must have started after the match was created
                    .OrderByDescending(x => x.StartTime) // Grab the most recent match
                    .Select(x => x.MatchID)
                    .FirstOrDefaultAsync(ct);

                if (matchFound != default)
                {
                    match.MatchID = matchFound;
                    match.Active = false;
                    await context.SaveChangesAsync(ct);

                    // TODO: Adjust player MMR
                }
            }

            await transaction.CommitAsync(ct);
        }
        finally
        {
            _sempaphoreSlim.Release();
        }
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
                Name = $"Team Eagle (Avg MMR: 0)",
                Value = string.IsNullOrWhiteSpace(str) ? "No players" : str,
                Inline = true,
            });

            team = match.AwayTeam;
            players = team.Players.Select(player => $"{player.DiscordUser.DiscordUsername} (MMR: {player.DiscordUser.MMR})");
            str = string.Join("\n", players);
            matchEmbed.AddFields(new EmbedFieldProperties()
            {
                Name = $"Team Eagle (Avg MMR: 0)",
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

    private Rank GetPlayerRank(DiscordUser player, Rank[] ranks)
    {
        // Find the highest rank that the player qualifies for based on their MMR
        var ordered = ranks.OrderByDescending(x => x.MmrThreshold);
        return ordered.FirstOrDefault(rank => player.MMR >= rank.MmrThreshold) ?? ordered.LastOrDefault()
            ?? throw new Exception("There are no ranks");
    }

    // We must refresh queue display when queue event is update, match event is updated, or match event is ended
    // currently have it hooked to player joining / leaving the queue...
}
