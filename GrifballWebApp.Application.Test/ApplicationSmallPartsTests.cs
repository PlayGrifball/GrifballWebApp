using System.Text.Json;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.QueryableExtensions;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// The Application layer's small parts: DbContextExtensions.DiscardPendingChanges, PaginationExtensions, SeasonRefDto.PlayedByAsync
/// (against the fake IApplicationDbContext), and the DTOs and settings whose services can't fill them here (the team
/// pages' and the season teams' projections need SQL APPLY) or that are only serialized or bound (the on-demand job
/// status, the Discord settings, the commissioner dashboard).
/// </summary>
[TestFixture]
internal sealed class ApplicationSmallPartsTests : ApplicationDbContextTests
{
    private static readonly DateTime At = new(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
    private static readonly int[] SecondPage = [3, 4];
    private static readonly int[] OlderSeasonFirst = [2, 1];
    private static readonly int[] OnlyOlderSeason = [2];
    private static readonly int[] UsersOneAndTwo = [1, 2];
    private static readonly int[] AskedUsers = [1, 2, 2, 3];

    [Test]
    public async Task DiscardPendingChanges_UndoesAddsEditsAndDeletes_KeepsTheRest()
    {
        var edited = User.Seed(userName: "edited", displayName: "Before", id: 1);
        var deleted = User.Seed(userName: "deleted", id: 2);
        var untouched = User.Seed(userName: "untouched", id: 3);
        Context.Users.AddRange(edited, deleted, untouched);
        await Context.SaveChangesAsync();
        edited.Rename("After");
        Context.Users.Remove(deleted);
        var added = User.Seed(userName: "added", id: 4);
        Context.Users.Add(added);

        Context.DiscardPendingChanges();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Context.Entry(added).State, Is.EqualTo(EntityState.Detached));
            Assert.That(Context.Entry(edited).State, Is.EqualTo(EntityState.Unchanged));
            Assert.That(edited.DisplayName, Is.EqualTo("Before"));
            Assert.That(Context.Entry(deleted).State, Is.EqualTo(EntityState.Unchanged));
            Assert.That(Context.Entry(untouched).State, Is.EqualTo(EntityState.Unchanged));
            Assert.That(await Context.SaveChangesAsync(), Is.Zero);
            Assert.That(await Context.Users.CountAsync(), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task PaginationResult_NoRows_EmptyPage()
    {
        var page = await Context.Users.OrderBy(u => u.Id).Select(u => u.Id).PaginationResult(new PaginationFilter(1, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TotalCount, Is.Zero);
            Assert.That(page.Results, Is.Empty);
        }
    }

    [Test]
    public async Task PaginationResult_SecondPage_SkipsTheFirstAndCountsAll()
    {
        Context.Users.AddRange(Enumerable.Range(1, 5).Select(id => User.Seed(userName: $"user{id}", id: id)));
        await Context.SaveChangesAsync();

        var page = await Context.Users.OrderBy(u => u.Id).Select(u => u.Id).PaginationResult(new PaginationFilter(2, 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TotalCount, Is.EqualTo(5));
            Assert.That(page.Results, Is.EqualTo(SecondPage));
        }
    }

    [Test]
    public async Task PlayedByAsync_NoUsers_Empty()
    {
        var played = await SeasonRefDto.PlayedByAsync(Context, [], CancellationToken.None);

        Assert.That(played, Is.Empty);
    }

    [Test]
    public async Task PlayedByAsync_StatsRostersAndTitles_EachSeasonOnceOldestFirst()
    {
        var newer = Season.Seed(seasonName: "Newer", seasonID: 1);
        newer.Schedule(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var older = Season.Seed(seasonName: "Older", seasonID: 2);
        older.Schedule(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 2, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.AddRange(newer, older);
        Context.Users.AddRange(User.Seed(userName: "one", id: 1), User.Seed(userName: "two", id: 2), User.Seed(userName: "three", id: 3));
        Context.Teams.AddRange(Team.Seed(teamName: "New Team", seasonID: 1, teamID: 10), Team.Seed(teamName: "Old Team", seasonID: 2, teamID: 20));
        await Context.SaveChangesAsync();
        Context.AddRange(
            PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "One", GamesPlayed = 1 }, seasonID: 1, userID: 1, gameVersionID: 6),
            PlayerChampionship.Seed(seasonID: 1, teamID: 10, userID: 1, tier: "Pro", gamertagUsed: "One"),
            TeamPlayer.Seed(teamID: 20, userID: 1),
            TeamPlayer.Seed(teamID: 20, userID: 2));
        await Context.SaveChangesAsync();

        var played = await SeasonRefDto.PlayedByAsync(Context, AskedUsers, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(played.Keys, Is.EquivalentTo(UsersOneAndTwo));
            Assert.That(played[1].Select(s => s.SeasonID), Is.EqualTo(OlderSeasonFirst));
            Assert.That(played[1][0].SeasonName, Is.EqualTo("Older"));
            Assert.That(played[1][0].Year, Is.EqualTo(2024));
            Assert.That(played[1][1].Year, Is.EqualTo(2025));
            Assert.That(played[2].Select(s => s.SeasonID), Is.EqualTo(OnlyOlderSeason));
        }
    }

    [Test]
    public void OnDemandJobStatus_SerializesTheStateAsTextAndKeepsTheReportOffThePage()
    {
        var status = new OnDemandJobStatusDto
        {
            State = OnDemandJobState.Running,
            Progress = 40,
            ProgressMessage = "Pulling matches",
            StartedAt = At,
            LastRun = new OnDemandRunDto
            {
                FiredAt = At.AddDays(-1),
                DurationMs = 1500,
                Result = "Failed",
                Error = "timeout",
                Summary = "secret summary",
                MetricsJson = "{\"pulled\":3}",
            },
        };

        var json = JsonSerializer.Serialize(status);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var lastRun = root.GetProperty("LastRun");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("State").GetString(), Is.EqualTo("Running"));
            Assert.That(root.GetProperty("Progress").GetInt32(), Is.EqualTo(40));
            Assert.That(root.GetProperty("ProgressMessage").GetString(), Is.EqualTo("Pulling matches"));
            Assert.That(root.GetProperty("StartedAt").GetDateTime(), Is.EqualTo(At));
            Assert.That(lastRun.GetProperty("Result").GetString(), Is.EqualTo("Failed"));
            Assert.That(lastRun.GetProperty("Error").GetString(), Is.EqualTo("timeout"));
            Assert.That(lastRun.GetProperty("DurationMs").GetDouble(), Is.EqualTo(1500));
            Assert.That(lastRun.TryGetProperty("Summary", out _), Is.False);
            Assert.That(lastRun.TryGetProperty("MetricsJson", out _), Is.False);
            Assert.That(status.LastRun!.Summary, Is.EqualTo("secret summary"));
            Assert.That(status.LastRun.MetricsJson, Is.EqualTo("{\"pulled\":3}"));
            Assert.That(status.LastRun.FiredAt, Is.EqualTo(At.AddDays(-1)));
        }
    }

    [Test]
    public void OnDemandJobState_ReadsFromText()
    {
        var status = JsonSerializer.Deserialize<OnDemandJobStatusDto>("{\"State\":\"Queued\"}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status!.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(status.LastRun, Is.Null);
        }
    }

    [Test]
    public void DiscordOptions_Defaults()
    {
        var options = new DiscordOptions();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.DisableGlobally, Is.False);
            Assert.That(options.MatchPlayers, Is.EqualTo(8));
            Assert.That(options.KFactor, Is.EqualTo(32));
            Assert.That(options.WinThreshold, Is.EqualTo(3));
            Assert.That(options.BonusPerWin, Is.EqualTo(10));
            Assert.That(options.MaxBonus, Is.EqualTo(20));
            Assert.That(options.LossThreshold, Is.EqualTo(3));
            Assert.That(options.PenaltyPerLoss, Is.EqualTo(10));
            Assert.That(options.MaxPenalty, Is.EqualTo(20));
        }
    }

    [Test]
    public void DiscordOptions_BindFromTheDiscordSection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Discord:ClientId"] = "client",
            ["Discord:ClientSecret"] = "secret",
            ["Discord:Token"] = "token",
            ["Discord:DraftChannel"] = "1",
            ["Discord:DisableGlobally"] = "true",
            ["Discord:QueueChannel"] = "2",
            ["Discord:EventsChannel"] = "3",
            ["Discord:ReschedulesChannel"] = "4",
            ["Discord:LogChannel"] = "5",
            ["Discord:MatchPlayers"] = "4",
            ["Discord:KFactor"] = "16",
            ["Discord:WinThreshold"] = "2",
            ["Discord:BonusPerWin"] = "5",
            ["Discord:MaxBonus"] = "15",
            ["Discord:LossThreshold"] = "4",
            ["Discord:PenaltyPerLoss"] = "6",
            ["Discord:MaxPenalty"] = "12",
        }).Build();

        var options = configuration.GetSection("Discord").Get<DiscordOptions>()!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((options.ClientId, options.ClientSecret, options.Token), Is.EqualTo(("client", "secret", "token")));
            Assert.That((options.DraftChannel, options.QueueChannel, options.EventsChannel, options.ReschedulesChannel, options.LogChannel),
                Is.EqualTo((1UL, 2UL, 3UL, 4UL, 5UL)));
            Assert.That(options.DisableGlobally, Is.True);
            Assert.That((options.MatchPlayers, options.KFactor, options.WinThreshold, options.BonusPerWin, options.MaxBonus),
                Is.EqualTo((4, 16, 2, 5, 15)));
            Assert.That((options.LossThreshold, options.PenaltyPerLoss, options.MaxPenalty), Is.EqualTo((4, 6, 12)));
        }
    }

    [Test]
    public void CommissionerDashboard_StartsEmpty_AndHoldsWhatItIsGiven()
    {
        var empty = new CommissionerDashboardDto();
        var dashboard = new CommissionerDashboardDto
        {
            PendingReschedules = [new RescheduleDto { MatchRescheduleID = 1 }],
            OverdueMatches = [new OverdueMatchDto { SeasonMatchID = 2, HoursOverdue = 30 }],
            Summary = new DashboardSummaryDto { PendingRescheduleCount = 1, OverdueMatchCount = 1, CriticalOverdueCount = 0 },
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty.PendingReschedules, Is.Empty);
            Assert.That(empty.OverdueMatches, Is.Empty);
            Assert.That((empty.Summary.PendingRescheduleCount, empty.Summary.OverdueMatchCount, empty.Summary.CriticalOverdueCount), Is.EqualTo((0, 0, 0)));
            Assert.That(dashboard.PendingReschedules.Single().MatchRescheduleID, Is.EqualTo(1));
            Assert.That(dashboard.OverdueMatches.Single().HoursOverdue, Is.EqualTo(30));
            Assert.That((dashboard.Summary.PendingRescheduleCount, dashboard.Summary.OverdueMatchCount, dashboard.Summary.CriticalOverdueCount), Is.EqualTo((1, 1, 0)));
        }
    }

    [Test]
    public void SeasonTeamDto_DefaultsAndValues()
    {
        var empty = new SeasonTeamDto();
        var member = new SeasonTeamMemberDto
        {
            TeamPlayerID = 5, UserID = 6, Gamertag = "Six", IsCaptain = true, DraftCaptainOrder = 1, DraftRound = 2, DraftPick = 3, Region = "Europe",
        };
        var team = new SeasonTeamDto
        {
            TeamID = 10, TeamName = "Bombers", Wins = 4, Losses = 1, TBD = 2, RecordFromSource = true, Titles = ["Pro"], DraftCaptainOrder = 1, Players = [member],
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That((empty.TeamName, empty.Titles.Count, empty.Players.Count, empty.DraftCaptainOrder), Is.EqualTo(("", 0, 0, (int?)null)));
            Assert.That((team.TeamID, team.TeamName, team.Wins, team.Losses, team.TBD, team.RecordFromSource), Is.EqualTo((10, "Bombers", 4, 1, 2, true)));
            Assert.That((team.Titles.Single(), team.DraftCaptainOrder), Is.EqualTo(("Pro", (int?)1)));
            var player = team.Players.Single();
            Assert.That((player.TeamPlayerID, player.UserID, player.Gamertag, player.IsCaptain), Is.EqualTo((5, 6, "Six", true)));
            Assert.That((player.DraftCaptainOrder, player.DraftRound, player.DraftPick, player.Region), Is.EqualTo(((int?)1, (int?)2, (int?)3, "Europe")));
        }
    }

    [Test]
    public void TeamHistoryDtos_HoldAFranchiseAndItsSeasons()
    {
        var season = new TeamHistorySeasonDto
        {
            TeamID = 10,
            TeamName = "Bombers",
            Season = new SeasonInfoDto { SeasonID = 1, SeasonName = "S1" },
            Wins = 4,
            Losses = 1,
            RecordFromSource = true,
            Titles = ["Pro"],
            Players = [],
        };
        var franchise = new TeamHistoryDto
        {
            TeamName = "Bombers",
            LeagueName = "AGLA",
            Appearances = 1,
            Wins = 4,
            Losses = 1,
            Championships = 1,
            Players = 5,
            Seasons = [season],
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That((franchise.TeamName, franchise.LeagueName, franchise.Appearances), Is.EqualTo(("Bombers", "AGLA", 1)));
            Assert.That((franchise.Wins, franchise.Losses, franchise.Championships, franchise.Players), Is.EqualTo((4, 1, 1, 5)));
            var only = franchise.Seasons.Single();
            Assert.That((only.TeamID, only.TeamName, only.Season.SeasonName, only.Wins, only.Losses, only.RecordFromSource), Is.EqualTo((10, "Bombers", "S1", 4, 1, true)));
            Assert.That((only.Titles.Single(), only.Players.Length), Is.EqualTo(("Pro", 0)));
        }
    }
}
