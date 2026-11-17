using GrifballWebApp.Application.Draft;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The draft helper (<see cref="DraftHelperService"/>): the player pool with what a captain weighs (careers, timeslots, region),
/// and the picks suggested for a team, ranked by a fixed score, each with its reasons.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DraftHelperServiceTests
{
    private const int West = 1, East = 3;
    private static readonly string[] ByName = ["Ace", "Bench", "Mid", "Rookie"];
    private GrifballContext _context = null!;
    private Season _season = null!;
    private Team _reds = null!;
    private User _cap = null!, _ace = null!, _mid = null!, _bench = null!, _rookie = null!;
    private int _monday, _tuesday;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private DraftHelperService Service() => new(_context, new LeaderboardsService(_context, new SeasonStatsService(_context), new MatchmakingStatsService(_context),
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<LeaderboardsService>.Instance));

    /// <summary>
    /// A past season with Ace (16 of 20 won, the most goals and kills), Mid (10 of 20) and Bench (4 of 20); Rookie never played.
    /// The season drafting plays Mondays and Tuesdays at 20:00: Reds (captain Cap, East, Mondays) is the only team, and short of
    /// players on both. Ace (West) can play Mondays; Mid and Bench (East) both; Rookie (East) Mondays.
    /// </summary>
    private async Task Seed(bool capPlaysBoth = false)
    {
        var old = await _context.AddSeason("Old");
        _season = await _context.AddSeason("Now", captainsLocked: true);
        AvailabilityOption mondays = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(20, 0)),
            tuesdays = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Tuesday, time: new TimeOnly(20, 0));
        _context.Availability.AddRange(mondays, tuesdays);
        await _context.SaveChangesAsync();
        (_monday, _tuesday) = (mondays.AvailabilityOptionID, tuesdays.AvailabilityOptionID);
        _context.AddRange(SeasonAvailability.Seed(_season.SeasonID, _monday), SeasonAvailability.Seed(_season.SeasonID, _tuesday));

        _cap = await Player("cap", "Cap", East, capPlaysBoth ? [_monday, _tuesday] : [_monday]);
        _ace = await Player("ace", "Ace", West, [_monday]);
        _mid = await Player("mid", "Mid", East, [_monday, _tuesday]);
        _bench = await Player("bench", "Bench", East, [_monday, _tuesday]);
        _rookie = await Player("rookie", "Rookie", East, [_monday]);
        _reds = await _context.AddTeam(_season, "Reds", _cap, 1);

        _context.PlayerSeasonStats.AddRange(
            Stats(old, _ace, wins: 16, goals: 40, kills: 300),
            Stats(old, _mid, wins: 10, goals: 20, kills: 200),
            Stats(old, _bench, wins: 4, goals: 4, kills: 80));
        await _context.SaveChangesAsync();
    }

    private async Task<User> Player(string userName, string? name, int region, int[] timeslots)
    {
        var user = await _context.AddUser(userName, name);
        user.ChooseRegion(region, RegionSource.User);
        _context.SeasonSignups.Add(SeasonSignup.Seed(_season.SeasonID, user.Id, timestamp: new DateTime(2024, 12, 1), timeslots: timeslots));
        await _context.SaveChangesAsync();
        return user;
    }

    private static PlayerSeasonStat Stats(Season season, User user, int wins, int goals, int kills, int games = 20)
        => PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = user.DisplayName!, GamesPlayed = games, Wins = wins, Losses = games - wins, Goals = goals, Kills = kills });

    [Test]
    public async Task Pool_HasEachPlayerLeft_WithTheirCareer_Timeslots_AndRegion()
    {
        await Seed();

        var pool = await Service().PoolAsync(_season.SeasonID);

        var ace = pool.Single(p => p.UserID == _ace.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pool.Select(p => p.Name), Is.EqualTo(ByName), "By name; not the captain, on a team");
            Assert.That((ace.Region, ace.WillCaptain), Is.EqualTo(("West North America", false)));
            Assert.That(ace.Timeslots.Select(t => (t.DayOfWeek, t.Time)), Is.EqualTo(new[] { (DayOfWeek.Monday, new TimeOnly(20, 0)) }));
            Assert.That((ace.Career!.Games, ace.Career.WinPercent, ace.Career.Seasons), Is.EqualTo((20, 80.0m, 1)));
            Assert.That(pool.Single(p => p.UserID == _rookie.Id).Career, Is.Null, "Never played");
        }
    }

    [Test]
    public async Task Pool_NamesAPlayerWithNoName_AnUnknownSpartan()
    {
        await Seed();
        await Player("nameless", null, East, [_monday]);

        var pool = await Service().PoolAsync(_season.SeasonID);

        Assert.That(pool.Select(p => p.Name), Has.Member("Unknown Spartan"));
    }

    [Test]
    public async Task Suggest_RanksThePool_ForTheTeamOnDeck_ByPerformance_Fit_AndRegion_EachWithWhy()
    {
        await Seed();

        var suggestions = (await Service().SuggestAsync(_season.SeasonID))!;

        var mid = suggestions.Picks[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((suggestions.TeamID, suggestions.CaptainName), Is.EqualTo((_reds.TeamID, "Cap")));
            // Mid: middling career, fills both short slots, same region (0.75); Ace: best career, one slot, other region (0.675);
            // Rookie: unknown career, one slot, same region (0.575); Bench: worst career, both slots, same region (0.5)
            Assert.That(suggestions.Picks.Select(p => p.UserID), Is.EqualTo(new[] { _mid.Id, _ace.Id, _rookie.Id, _bench.Id }));
            Assert.That(mid.Score, Is.EqualTo(0.75).Within(1e-9));
            Assert.That(mid.Reasons, Has.Some.Contains("2 of the 2 timeslots the team is short of"));
            Assert.That(mid.Reasons, Has.Some.Contains("Same region as the captain"));
            Assert.That(suggestions.Picks[1].Reasons, Has.Some.Contains("80% of 20 games"));
            Assert.That(suggestions.Picks[2].Reasons, Has.Some.Contains("No recorded games"));
        }
    }

    [Test]
    public async Task Suggest_ForATeamShortOfNoTimeslot_FitsByItsOwn_WithTitles_AndFewGamesAnUnknown()
    {
        await Seed(capPlaysBoth: true);
        // One player a slot is enough: Cap alone plays both, so the team is short of neither
        var rules = SeasonSchedulingRules.Defaults(_season.SeasonID);
        rules.Change(1, 1, 2, null);
        _context.SeasonSchedulingRules.Add(rules);
        // Ace won two past titles, Bench one; Rookie played 3 games, too few to count
        var old = await _context.Seasons.SingleAsync(s => s.SeasonName == "Old");
        var older = await _context.AddSeason("Older");
        await _context.SeedChampionsAsync(old.SeasonID, "Open", (await _context.AddTeam(old, "Champs", _ace, 1, _bench)).TeamID);
        await _context.SeedChampionsAsync(older.SeasonID, "Open", (await _context.AddTeam(older, "Champs Again", _ace, 1)).TeamID);
        _context.PlayerSeasonStats.Add(Stats(older, _rookie, wins: 2, goals: 1, kills: 10, games: 3));
        await _context.SaveChangesAsync();

        var picks = (await Service().SuggestAsync(_season.SeasonID, count: 10))!.Picks.ToDictionary(p => p.UserID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(picks[_ace.Id].Reasons, Has.Some.Contains(", 2 titles"));
            Assert.That(picks[_ace.Id].Reasons, Has.Some.Contains("Can play 1 of the team's 2 timeslots"));
            Assert.That(picks[_bench.Id].Reasons, Has.Some.Contains(", a title"));
            Assert.That(picks[_mid.Id].Reasons, Has.Some.Contains("Can play 2 of the team's 2 timeslots"));
            Assert.That(picks[_rookie.Id].Reasons, Has.Some.EqualTo("Only 3 recorded games: an unknown"));
        }
    }

    [Test]
    public async Task Suggest_HowMany_AndForATeamNamed_AndNoneWithoutATeam()
    {
        await Seed();
        var empty = await _context.AddSeason("Empty", captainsLocked: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Service().SuggestAsync(_season.SeasonID, count: 2))!.Picks, Has.Count.EqualTo(2));
            Assert.That((await Service().SuggestAsync(_season.SeasonID, teamID: _reds.TeamID))!.TeamID, Is.EqualTo(_reds.TeamID));
            Assert.That(await Service().SuggestAsync(empty.SeasonID), Is.Null, "No team on deck");
            Assert.That(await Service().SuggestAsync(_season.SeasonID, teamID: 999_999), Is.Null, "No such team in the season");
        }
    }

    [Test]
    public async Task TheDiscordOnDeckMessage_ListsTheSuggestedPicks_WithAMenuOfThem_BeforeThePool()
    {
        await Seed();
        var teams = new GrifballWebApp.Application.Teams.TeamService(_context, NSubstitute.Substitute.For<GrifballWebApp.Application.Notifications.IPublisher>(),
            NSubstitute.Substitute.For<GrifballWebApp.Application.Teams.IDraftChannel>(), NSubstitute.Substitute.For<GrifballWebApp.Application.Database.IDatabaseLocks>());

        await new GrifballWebApp.Infrastructure.Outbox.ContextOutbox(_context).EnqueueOnDeckAsync(_context, teams, _season.SeasonID, Service(), CancellationToken.None);
        await _context.SaveChangesAsync();

        var row = await _context.Outbox.AsNoTracking().SingleAsync(m => m.Kind == GrifballWebApp.Infrastructure.Teams.DraftOutbox.OnDeck);
        var messages = System.Text.Json.JsonSerializer.Deserialize<GrifballWebApp.Infrastructure.Teams.DraftOnDeckPayload>(row.Payload, GrifballWebApp.Infrastructure.Outbox.OutboxJson.Options)!.Messages;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages[0].Content, Does.StartWith("Cap is on deck. Suggested picks:\n1. Mid: "));
            Assert.That(messages[0].Menus.Single().Placeholder, Is.EqualTo(GrifballWebApp.Infrastructure.Teams.DraftOutbox.SuggestedPlaceholder));
            Assert.That(messages[0].Menus.Single().Options.Select(o => o.Value), Is.EqualTo(new[] { _mid.Id, _ace.Id, _rookie.Id, _bench.Id }.Select(i => i.ToString())));
            Assert.That(messages[1].Content, Is.Null, "The pool after, as before");
            Assert.That(messages[1].Menus.Single().Options, Has.Count.EqualTo(4));
        }
    }
}
