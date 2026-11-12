using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// CommandLookups's unit tests, on a real GrifballContext over a temp-file SQLite database (#402): no database server, no
/// host, no network. Its queries are too tied to the database to run over a substitute one.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommandLookupsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private CommandLookups Lookups() => new(Db, new SeasonService(Db, null!, new FixedTimeProvider(Now)));

    [Test]
    public async Task Season_WithNoInput_AndNoSeasons_ReturnsNull()
    {
        Assert.That(await Lookups().Season(null), Is.Null);
    }

    [Test]
    public async Task Season_WithNoInput_DefaultCurrent_AndARunningSeason_ReturnsIt()
    {
        var running = await AddSeason("Running");
        running.WithDates(signupsOpen: Now.AddDays(-30), signupsClose: Now.AddDays(-20), draftStart: Now.AddDays(-10), seasonStart: Now.AddDays(-1), seasonEnd: Now.AddDays(30));
        await AddSeason("Latest"); // not running, not started: the latest by ID when none is running
        await Db.SaveChangesAsync();

        var result = await Lookups().Season(null, SeasonDefault.Current);

        Assert.That(result, Is.EqualTo(running.SeasonID));
    }

    [Test]
    public async Task Season_WithNoInput_DefaultCurrent_AndNoneRunning_ReturnsTheLatestByID()
    {
        await AddSeason("Older");
        var newer = await AddSeason("Newer");

        var result = await Lookups().Season(null, SeasonDefault.Current);

        Assert.That(result, Is.EqualTo(newer.SeasonID));
    }

    [Test]
    public async Task Season_WithNoInput_DefaultUpcoming_AndAnUpcomingSeason_ReturnsTheEarliestUpcoming()
    {
        var later = await AddSeason("Later");
        later.WithDates(signupsOpen: Now.AddDays(10), signupsClose: Now.AddDays(20), draftStart: Now.AddDays(25), seasonStart: Now.AddDays(60), seasonEnd: Now.AddDays(90));
        var sooner = await AddSeason("Sooner");
        sooner.WithDates(signupsOpen: Now.AddDays(1), signupsClose: Now.AddDays(5), draftStart: Now.AddDays(8), seasonStart: Now.AddDays(30), seasonEnd: Now.AddDays(60));
        await Db.SaveChangesAsync();

        var result = await Lookups().Season(null, SeasonDefault.Upcoming);

        Assert.That(result, Is.EqualTo(sooner.SeasonID));
    }

    [Test]
    public async Task Season_WithNoInput_DefaultUpcoming_AndNoneUpcoming_FallsBackToCurrent()
    {
        var running = await AddSeason("Running");
        running.WithDates(signupsOpen: Now.AddDays(-30), signupsClose: Now.AddDays(-20), draftStart: Now.AddDays(-10), seasonStart: Now.AddDays(-1), seasonEnd: Now.AddDays(30));
        await Db.SaveChangesAsync();

        var result = await Lookups().Season(null, SeasonDefault.Upcoming);

        Assert.That(result, Is.EqualTo(running.SeasonID));
    }

    [Test]
    public async Task Season_ByPickedChoice_ReturnsThatID_WhenItExists()
    {
        var season = await AddSeason("Winter");

        Assert.That(await Lookups().Season(CommandLookups.ChoiceValue(season.SeasonID)), Is.EqualTo(season.SeasonID));
    }

    [Test]
    public async Task Season_ByPickedChoice_ReturnsNull_WhenItDoesNotExist()
    {
        Assert.That(await Lookups().Season(CommandLookups.ChoiceValue(999)), Is.Null);
    }

    [Test]
    public async Task Season_ByExactName_ReturnsIt()
    {
        var season = await AddSeason("Winter 2026");

        Assert.That(await Lookups().Season("Winter 2026"), Is.EqualTo(season.SeasonID));
    }

    [Test]
    public async Task Season_ByUniquePartialName_ReturnsIt()
    {
        var season = await AddSeason("Winter 2026");

        Assert.That(await Lookups().Season("inter"), Is.EqualTo(season.SeasonID));
    }

    [Test]
    public async Task Season_ByAmbiguousPartialName_ReturnsNull()
    {
        await AddSeason("Winter 2025");
        await AddSeason("Winter 2026");

        Assert.That(await Lookups().Season("Winter"), Is.Null);
    }

    [Test]
    public async Task Season_ByDigitsThatMatchNoName_ButAreAnID_ReturnsIt_WhenItExists()
    {
        var season = await AddSeason("Winter");

        Assert.That(await Lookups().Season(season.SeasonID.ToString()), Is.EqualTo(season.SeasonID));
    }

    [Test]
    public async Task Season_ByTextThatMatchesNoNameAndIsNotAnID_ReturnsNull()
    {
        await AddSeason("Winter");

        Assert.That(await Lookups().Season("nonsense"), Is.Null);
    }

    [Test]
    public async Task SeasonFor_ReturnsTheID_WhenFound()
    {
        var season = await AddSeason("Winter");
        var discord = new CommandContextRecorder();

        var result = await Lookups().SeasonFor(discord.Context, "Winter");

        Assert.That(result, Is.EqualTo(season.SeasonID));
        Assert.That(discord.Edits, Is.Empty);
    }

    [Test]
    public async Task SeasonFor_RepliesNoSuchSeason_WhenNotFound()
    {
        var discord = new CommandContextRecorder();

        var result = await Lookups().SeasonFor(discord.Context, "nonsense");

        Assert.That(result, Is.Null);
        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchSeason));
    }

    [Test]
    public async Task Team_ByExactName_ReturnsItsSeasonAndTeamID()
    {
        var season = await AddSeason("Winter");
        var team = await AddTeam(season, "Grifters");

        var result = await Lookups().Team("Grifters");

        Assert.That(result, Is.EqualTo((season.SeasonID, team.TeamID)));
    }

    [Test]
    public async Task Team_ByPickedChoice_ReturnsIt()
    {
        var season = await AddSeason("Winter");
        var team = await AddTeam(season, "Grifters");

        var result = await Lookups().Team(CommandLookups.ChoiceValue(team.TeamID));

        Assert.That(result, Is.EqualTo((season.SeasonID, team.TeamID)));
    }

    [Test]
    public async Task Team_ByAmbiguousPartialName_ReturnsNull()
    {
        var season = await AddSeason("Winter");
        await AddTeam(season, "Grifters One");
        await AddTeam(season, "Grifters Two");

        Assert.That(await Lookups().Team("Grifters"), Is.Null);
    }

    [Test]
    public async Task Team_ThatDoesNotExist_ReturnsNull()
    {
        Assert.That(await Lookups().Team("nonsense"), Is.Null);
    }

    [Test]
    public async Task Player_ByGamertag_ReturnsTheUserID()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");

        Assert.That(await Lookups().Player("Grifter1"), Is.EqualTo(user.Id));
    }

    [Test]
    public async Task Player_ByPickedChoice_ReturnsIt()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");

        Assert.That(await Lookups().Player(CommandLookups.ChoiceValue(user.Id)), Is.EqualTo(user.Id));
    }

    [Test]
    public async Task Player_ThatDoesNotExist_ReturnsNull()
    {
        Assert.That(await Lookups().Player("nonsense"), Is.Null);
    }

    [Test]
    public async Task SeasonMatch_ByTeamName_ReturnsTheOnlyMatchForThatTeam()
    {
        var season = await AddSeason("Winter");
        var home = await AddTeam(season, "Home Team");
        var away = await AddTeam(season, "Away Team");
        var match = await AddSeasonMatch(season, home, away);

        Assert.That(await Lookups().SeasonMatch("Home Team"), Is.EqualTo(match.SeasonMatchID));
    }

    [Test]
    public async Task SeasonMatch_ByPickedChoice_ReturnsIt()
    {
        var season = await AddSeason("Winter");
        var home = await AddTeam(season, "Home Team");
        var away = await AddTeam(season, "Away Team");
        var match = await AddSeasonMatch(season, home, away);

        Assert.That(await Lookups().SeasonMatch(CommandLookups.ChoiceValue(match.SeasonMatchID)), Is.EqualTo(match.SeasonMatchID));
    }

    [Test]
    public async Task SeasonMatch_ThatDoesNotExist_ReturnsNull()
    {
        Assert.That(await Lookups().SeasonMatch("nonsense"), Is.Null);
    }

    [Test]
    public async Task SeasonMatchFor_ReturnsTheID_WhenFound()
    {
        var season = await AddSeason("Winter");
        var home = await AddTeam(season, "Home Team");
        var away = await AddTeam(season, "Away Team");
        var match = await AddSeasonMatch(season, home, away);
        var discord = new CommandContextRecorder();

        var result = await Lookups().SeasonMatchFor(discord.Context, "Home Team");

        Assert.That(result, Is.EqualTo(match.SeasonMatchID));
    }

    [Test]
    public async Task SeasonMatchFor_RepliesNoSuchMatch_WhenNotFound()
    {
        var discord = new CommandContextRecorder();

        var result = await Lookups().SeasonMatchFor(discord.Context, "nonsense");

        Assert.That(result, Is.Null);
        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchMatch));
    }

    [Test]
    public async Task SeasonChoices_ReturnsMatchingSeasons_NewestFirst()
    {
        var older = await AddSeason("Winter 2025");
        var newer = await AddSeason("Winter 2026");
        await AddSeason("Summer 2025");

        var choices = await Lookups().SeasonChoices("Winter");

        Assert.That(choices.Select(x => x.Value), Is.EqualTo(new[] { CommandLookups.ChoiceValue(newer.SeasonID), CommandLookups.ChoiceValue(older.SeasonID) }));
    }

    [Test]
    public async Task TeamChoices_ReturnsMatchingTeams_LabelledWithTheirSeason()
    {
        var season = await AddSeason("Winter 2026");
        var team = await AddTeam(season, "Grifters");

        var choices = await Lookups().TeamChoices("Grif");

        Assert.That(choices.Single().Name, Is.EqualTo("Grifters (Winter 2026)"));
    }

    [Test]
    public async Task PlayerChoices_ReturnsMatchingPlayers()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");

        var choices = await Lookups().PlayerChoices("Grif");

        Assert.That(choices.Single().Name, Is.EqualTo("Grifter1"));
        Assert.That(choices.Single().Value, Is.EqualTo(CommandLookups.ChoiceValue(user.Id)));
    }

    [Test]
    public async Task SeasonMatchChoices_ReturnsMatchingMatches()
    {
        var season = await AddSeason("Winter 2026");
        var home = await AddTeam(season, "Home Team");
        var away = await AddTeam(season, "Away Team");
        var match = await AddSeasonMatch(season, home, away);

        var choices = await Lookups().SeasonMatchChoices("Home");

        Assert.That(choices.Single().Name, Is.EqualTo("Home Team vs Away Team (Winter 2026)"));
        Assert.That(choices.Single().Value, Is.EqualTo(CommandLookups.ChoiceValue(match.SeasonMatchID)));
    }

    [TestCase("2026-11-20 20:00", true)]
    [TestCase("not a time", false)]
    public void TryParseUtc_ParsesInvariantDates(string input, bool expected)
    {
        Assert.That(CommandLookups.TryParseUtc(input, out _), Is.EqualTo(expected));
    }

    [Test]
    public void ChoiceName_CutsNamesLongerThan100Characters()
    {
        var long_ = new string('a', 150);

        var result = CommandLookups.ChoiceName(long_);

        Assert.That(result, Is.EqualTo(new string('a', 99) + "…"));
        Assert.That(result, Has.Length.EqualTo(100));
    }

    [Test]
    public void ChoiceName_LeavesAShortNameAlone()
    {
        Assert.That(CommandLookups.ChoiceName("Grifters"), Is.EqualTo("Grifters"));
    }

    [Test]
    public void ChoiceValue_PrefixesTheID()
    {
        Assert.That(CommandLookups.ChoiceValue(42), Is.EqualTo("id:42"));
    }
}
