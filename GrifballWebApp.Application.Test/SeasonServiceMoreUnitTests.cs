using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// SeasonService's season list (every filter, the sort, its players, champions and team names), its champions on the
/// season summary and its player stats (order, team filter, the row mapping of SeasonPlayerStatDto.From), beyond
/// SeasonServiceUnitTests. GetSeasonTeams' found path isn't here: its projection (Titles with Distinct and Players, two
/// nested collections) needs SQL APPLY, which the fake context's plain UseSqlite() lacks; Integration.Test covers it.
/// </summary>
[TestFixture]
internal sealed class SeasonServiceMoreUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int HaloInfinite = 6;
    private const int Halo3 = 1;
    private const int EndedSeasonID = 1;
    private const int CurrentSeasonID = 2;
    private const int FutureSeasonID = 3;
    private const int ZuluTeamID = 10;
    private const int AlphaTeamID = 11;
    private static readonly int[] LatestEndFirst = [CurrentSeasonID, EndedSeasonID];
    private static readonly int[] ByNameAscending = [EndedSeasonID, CurrentSeasonID];
    private static readonly int[] UsersByKillsThenName = [2, 1, 3];
    private static readonly int[] OnlyUser1 = [1];
    private static readonly int[] CaptainFirst = [2, 1];
    private static readonly string[] TeamNamesAlphabetical = ["Alpha Team", "zulu"];
    private static readonly string[] TiersInTitleOrder = ["Pro", "Amateur"];

    private SeasonService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var time = new FakeTimeProvider(Now);
        _service = new SeasonService(Context, new SeasonStatsService(Context, time), new GrifballWebApp.Application.Seasons.SeasonVisibility(Context, time), time);
    }

    private static StatSheetTotals Totals(string gamertag, int goals, int kills) => new()
    {
        Gamertag = gamertag,
        GamesPlayed = 10,
        Wins = 6,
        Losses = 4,
        Goals = goals,
        Kills = kills,
        Deaths = 20,
        Assists = 5,
        CarrierKills = 3,
        Sprees = 2,
        MultiKills = 1,
        Exterminations = 1,
        Punches = 4,
        SwordKills = 7,
    };

    /// <summary>
    /// An ended AGLA Infinite league season of 2025 (two teams: zulu with users 1 and 2, Alpha Team; titles: Pro to zulu,
    /// user 2 its captain, then Amateur to Alpha Team; stored stats for users 1, 2 and 3, user 3 on no team), a current
    /// GWL Halo 3 tournament of 2026, and one that hasn't started.
    /// </summary>
    private async Task SeedSeasons()
    {
        var ended = Season.Seed(seasonName: "Alpha", seasonID: EndedSeasonID, leagueID: 1, gameVersionID: HaloInfinite, format: SeasonFormat.LeagueSeason);
        ended.Schedule(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var current = Season.Seed(seasonName: "Bravo", seasonID: CurrentSeasonID, leagueID: 3, gameVersionID: Halo3, format: SeasonFormat.Tournament);
        current.Schedule(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var future = Season.Seed(seasonName: "Charlie", seasonID: FutureSeasonID, leagueID: 1, gameVersionID: HaloInfinite);
        future.Schedule(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.AddRange(ended, current, future);
        Context.Users.AddRange(User.Seed(userName: "one", id: 1), User.Seed(userName: "two", id: 2), User.Seed(userName: "three", id: 3));
        Context.Teams.AddRange(Team.Seed(teamName: "zulu", seasonID: EndedSeasonID, teamID: ZuluTeamID),
            Team.Seed(teamName: "Alpha Team", seasonID: EndedSeasonID, teamID: AlphaTeamID));
        await Context.SaveChangesAsync();

        Context.AddRange(TeamPlayer.Seed(teamID: ZuluTeamID, userID: 1), TeamPlayer.Seed(teamID: ZuluTeamID, userID: 2));
        Context.AddRange(
            PlayerSeasonStat.Seed(Totals("One", 30, 50), seasonID: EndedSeasonID, userID: 1, gameVersionID: HaloInfinite, teamID: ZuluTeamID,
                tradeNote: "from Alpha Team", roleNote: "slayer"),
            PlayerSeasonStat.Seed(Totals("Two", 30, 60), seasonID: EndedSeasonID, userID: 2, gameVersionID: HaloInfinite, teamID: ZuluTeamID),
            PlayerSeasonStat.Seed(Totals("Three", 10, 90), seasonID: EndedSeasonID, userID: 3, gameVersionID: HaloInfinite));
        await Context.SaveChangesAsync();

        Context.Add(PlayerChampionship.Seed(seasonID: EndedSeasonID, teamID: ZuluTeamID, userID: 1, tier: "Pro", gamertagUsed: "One"));
        await Context.SaveChangesAsync();
        Context.Add(PlayerChampionship.Seed(seasonID: EndedSeasonID, teamID: ZuluTeamID, userID: 2, tier: "Pro", gamertagUsed: "Two", isCaptain: true));
        await Context.SaveChangesAsync();
        Context.Add(PlayerChampionship.Seed(seasonID: EndedSeasonID, teamID: AlphaTeamID, userID: 3, tier: "Amateur", gamertagUsed: "Three"));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetSeasons_NoFilter_StartedSeasonsLatestEndFirst()
    {
        await SeedSeasons();

        var page = await _service.GetSeasons(new SeasonListFilter());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Results.Select(s => s.SeasonID), Is.EqualTo(LatestEndFirst));
        }
    }

    [TestCase(3, null, null, null, null, CurrentSeasonID)]
    [TestCase(null, HaloInfinite, null, null, null, EndedSeasonID)]
    [TestCase(null, null, SeasonFormat.Tournament, null, null, CurrentSeasonID)]
    [TestCase(null, null, null, 2025, null, EndedSeasonID)]
    [TestCase(null, null, null, null, false, CurrentSeasonID)]
    [TestCase(null, null, null, null, true, EndedSeasonID)]
    public async Task GetSeasons_EachFilter_KeepsOnlyItsSeason(int? leagueID, int? gameVersionID, SeasonFormat? format, int? year, bool? ended, int expected)
    {
        await SeedSeasons();

        var page = await _service.GetSeasons(new SeasonListFilter { LeagueID = leagueID, GameVersionID = gameVersionID, Format = format, Year = year, Ended = ended });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TotalCount, Is.EqualTo(1));
            Assert.That(page.Results.Single().SeasonID, Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task GetSeasons_SortedByName_UsesTheSort()
    {
        await SeedSeasons();

        var page = await _service.GetSeasons(new SeasonListFilter { SortColumn = "seasonName", SortDirection = SortDirection.Asc });

        Assert.That(page.Results.Select(s => s.SeasonID), Is.EqualTo(ByNameAscending));
    }

    [Test]
    public async Task GetSeasons_SortDirectionNone_KeepsTheDefaultOrder()
    {
        await SeedSeasons();

        var page = await _service.GetSeasons(new SeasonListFilter { SortColumn = "seasonName", SortDirection = SortDirection.None });

        Assert.That(page.Results.Select(s => s.SeasonID), Is.EqualTo(LatestEndFirst));
    }

    [Test]
    public async Task GetSeasons_FillsPlayersChampionsAndTeamNames()
    {
        await SeedSeasons();

        var page = await _service.GetSeasons(new SeasonListFilter());

        var ended = page.Results.Single(s => s.SeasonID == EndedSeasonID);
        var current = page.Results.Single(s => s.SeasonID == CurrentSeasonID);
        var pro = ended.Champions[0];
        using (Assert.EnterMultipleScope())
        {
            // Users 1 and 2 rostered and with stats, user 3 with stats only: each once
            Assert.That(ended.PlayerCount, Is.EqualTo(3));
            Assert.That(ended.TeamCount, Is.EqualTo(2));
            Assert.That(ended.TeamNames, Is.EqualTo(TeamNamesAlphabetical));
            Assert.That(ended.Champions.Select(c => c.Tier), Is.EqualTo(TiersInTitleOrder));
            Assert.That(pro.TeamID, Is.EqualTo(ZuluTeamID));
            Assert.That(pro.TeamName, Is.EqualTo("zulu"));
            Assert.That(pro.Roster.Select(p => p.UserID), Is.EqualTo(CaptainFirst));
            Assert.That(pro.Roster[0].Gamertag, Is.EqualTo("Two"));
            Assert.That(pro.Roster[0].IsCaptain, Is.True);
            Assert.That(pro.Roster[1].IsCaptain, Is.False);
            Assert.That(ended.Champions[1].Roster.Single().Gamertag, Is.EqualTo("Three"));
            Assert.That(current.PlayerCount, Is.Zero);
            Assert.That(current.Champions, Is.Empty);
            Assert.That(current.TeamNames, Is.Empty);
        }
    }

    [Test]
    public async Task GetSeasonSummary_SeasonWithTitlesAndStats_ReturnsChampionsAndStatCount()
    {
        await SeedSeasons();

        var summary = await _service.GetSeasonSummary(EndedSeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary!.SeasonName, Is.EqualTo("Alpha"));
            Assert.That(summary.TeamCount, Is.EqualTo(2));
            Assert.That(summary.PlayerStatCount, Is.EqualTo(3));
            Assert.That(summary.Champions.Select(c => c.Tier), Is.EqualTo(TiersInTitleOrder));
            Assert.That(summary.Champions[0].Roster.Select(p => p.UserID), Is.EqualTo(CaptainFirst));
        }
    }

    [Test]
    public async Task GetSeasonPlayerStats_MostGoalsThenKillsFirst()
    {
        await SeedSeasons();

        var stats = await _service.GetSeasonPlayerStats(EndedSeasonID);

        Assert.That(stats.Select(s => s.UserID), Is.EqualTo(UsersByKillsThenName));
    }

    [Test]
    public async Task GetSeasonPlayerStats_TeamFilter_KeepsOnlyThatTeamsRows()
    {
        await SeedSeasons();

        var stats = await _service.GetSeasonPlayerStats(EndedSeasonID, teamID: ZuluTeamID);

        Assert.That(stats.Select(s => s.UserID), Is.EquivalentTo(CaptainFirst));
    }

    [Test]
    public async Task GetSeasonPlayerStats_MapsEveryFieldOfTheStoredRow()
    {
        await SeedSeasons();

        var one = (await _service.GetSeasonPlayerStats(EndedSeasonID)).Single(s => s.UserID == 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(one.PlayerSeasonStatID, Is.GreaterThan(0));
            Assert.That(one.Gamertag, Is.EqualTo("One"));
            Assert.That(one.TeamID, Is.EqualTo(ZuluTeamID));
            Assert.That(one.TeamName, Is.EqualTo("zulu"));
            Assert.That(one.TradeNote, Is.EqualTo("from Alpha Team"));
            Assert.That(one.RoleNote, Is.EqualTo("slayer"));
            Assert.That(one.StintIndex, Is.EqualTo(1));
            Assert.That(one.IsPlayoffs, Is.False);
            Assert.That(one.IsCombined, Is.False);
            Assert.That(one.Source, Is.EqualTo("Recorded"));
            Assert.That(one.GamesPlayed, Is.EqualTo(10));
            Assert.That(one.Wins, Is.EqualTo(6));
            Assert.That(one.Losses, Is.EqualTo(4));
            Assert.That(one.WinPercent, Is.EqualTo(60.0m));
            Assert.That(one.Goals, Is.EqualTo(30));
            Assert.That(one.Kills, Is.EqualTo(50));
            Assert.That(one.Deaths, Is.EqualTo(20));
            Assert.That(one.Assists, Is.EqualTo(5));
            Assert.That(one.KdRatio, Is.EqualTo(2.5m));
            Assert.That(one.KdSpread, Is.EqualTo(30));
            Assert.That(one.CarrierKills, Is.EqualTo(3));
            Assert.That(one.Sprees, Is.EqualTo(2));
            Assert.That(one.MultiKills, Is.EqualTo(1));
            Assert.That(one.Exterminations, Is.EqualTo(1));
            Assert.That(one.Punches, Is.EqualTo(4));
            Assert.That(one.SwordKills, Is.EqualTo(7));
        }
    }
}
