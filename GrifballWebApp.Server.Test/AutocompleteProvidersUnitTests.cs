using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Commands;
using NetCord.Rest;
using System.Text.Json;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The slash commands' autocomplete providers, each offering what holds the text typed so far, on a temp-file SQLite
/// GrifballContext: a season (Winter) with teams Grifs and Rivals and a match between them, and players with gamertags.
/// The option typed is Discord's JSON for it; the interaction context isn't read.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AutocompleteProvidersUnitTests : SqliteGrifballContextFixture
{
    private static readonly string[] WinterOnly = ["Winter"];
    private static readonly string[] GrifsInWinter = ["Grifs (Winter)"];
    private static readonly string[] TheMatchInWinter = ["Grifs vs Rivals (Winter)"];
    private static readonly string[] HoldingGrif = ["Grifter1", "TheGrifter"];
    private static readonly string[] TheRenamedAccount = ["id:7001"];
    private static readonly string[] TheEuropes = ["North Europe", "South Europe", "Europe", "None (clear it)"];

    private static NetCord.ApplicationCommandInteractionDataOption Typed(string value)
        => new(JsonSerializer.Deserialize<NetCord.JsonModels.JsonApplicationCommandInteractionDataOption>(
            JsonSerializer.Serialize(new { name = "option", type = 3, value, focused = true }))!);

    private static IEnumerable<string> Names(IEnumerable<ApplicationCommandOptionChoiceProperties>? choices) => choices!.Select(c => c.Name);

    private async Task SeedLeague()
    {
        var season = await AddSeason("Winter");
        await AddSeason("Summer");
        var grifs = await AddTeam(season, "Grifs");
        var rivals = await AddTeam(season, "Rivals");
        await AddSeasonMatch(season, grifs, rivals);
        await AddUser(5, "Grifter1");
        await AddUser(6, "Other");
        await AddUser(7, "TheGrifter");
    }

    [Test]
    public async Task SeasonAutocomplete_OffersTheSeasonsNamed()
    {
        await SeedLeague();

        var choices = await new SeasonAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db))).GetChoicesAsync(Typed("Win"), null!);

        Assert.That(Names(choices), Is.EqualTo(WinterOnly));
    }

    [Test]
    public async Task TeamAutocomplete_OffersTheTeamsNamed_WithTheirSeason()
    {
        await SeedLeague();

        var choices = await new TeamAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db))).GetChoicesAsync(Typed("Gri"), null!);

        Assert.That(Names(choices), Is.EqualTo(GrifsInWinter));
    }

    [Test]
    public async Task PlayerAutocomplete_OffersThePlayersWhoseGamertagHoldsIt()
    {
        await SeedLeague();

        var choices = await new PlayerAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db))).GetChoicesAsync(Typed("Grif"), null!);

        Assert.That(Names(choices), Is.EqualTo(HoldingGrif));
    }

    [Test]
    public async Task SeasonMatchAutocomplete_OffersTheMatchesOfTheTeamsNamed()
    {
        await SeedLeague();

        var choices = await new SeasonMatchAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db))).GetChoicesAsync(Typed("Rivals"), null!);

        Assert.That(Names(choices), Is.EqualTo(TheMatchInWinter));
    }

    [Test]
    public async Task GamertagAutocomplete_OffersTheGamertagsHoldingWhatIsTyped()
    {
        await SeedLeague();

        var choices = await new GamertagAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db))).GetChoicesAsync(Typed("Grif"), null!);

        Assert.That(Names(choices), Is.EquivalentTo(HoldingGrif));
    }

    [Test]
    public async Task GamertagAutocomplete_OffersAPastGamertag_LabelledAsOne_AfterTheCurrentOnes_SentAsTheAccountsXUID()
    {
        var renamed = XboxUser.FromXbox(7001, "Brand New");
        renamed.RecordPastGamertag("Old Grif", null, null, GamertagHistorySource.Manual);
        // Found by its current gamertag: not listed again by its past one
        var current = XboxUser.FromXbox(7002, "Grif Case");
        current.RecordPastGamertag("Grif Older", null, null, GamertagHistorySource.Manual);
        Db.XboxUsers.AddRange(renamed, current, XboxUser.FromXbox(7003, "Grifter"));
        await Db.SaveChangesAsync();

        var choices = (await new GamertagAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db))).GetChoicesAsync(Typed("Grif"), null!))!.ToList();

        Assert.That(choices.Select(c => (c.Name, c.StringValue)), Is.EqualTo(new[]
        {
            ("Grif Case", "id:7002"),
            ("Grifter", "id:7003"),
            ("Old Grif (Previous Gamertag) · now Brand New", "id:7001"),
        }));
    }

    [Test]
    public async Task GamertagAutocomplete_SearchesPastGamertags_OnlyFromTwoCharactersTyped()
    {
        var renamed = XboxUser.FromXbox(7001, "Brand New");
        renamed.RecordPastGamertag("Old Grif", null, null, GamertagHistorySource.Manual);
        Db.XboxUsers.Add(renamed);
        await Db.SaveChangesAsync();
        var autocomplete = new GamertagAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)));

        var one = (await autocomplete.GetChoicesAsync(Typed("G"), null!))!.ToList();
        var two = (await autocomplete.GetChoicesAsync(Typed("Gr"), null!))!.ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(one, Is.Empty, "one character reads no gamertag history");
            Assert.That(two.Select(c => c.StringValue), Is.EqualTo(TheRenamedAccount));
        }
    }

    [Test]
    public async Task GamertagAutocomplete_ListsAGamertagTwoAccountsShare_Once_AsTheOneThatPlayedLast()
    {
        // 7101's row is stale: its account renamed away from "Shared Tag", which 7102 holds now and has played under
        Db.XboxUsers.AddRange(XboxUser.FromXbox(7101, "Shared Tag"), XboxUser.FromXbox(7102, "Shared Tag"));
        var game = GrifballWebApp.Domain.Halo.Match.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc), duration: TimeSpan.FromMinutes(9));
        game.AddTeam(0, 50, GrifballWebApp.Domain.Halo.Outcomes.Won);
        game.AddParticipant(0, 7102, new GrifballWebApp.Domain.Halo.MatchParticipantStats());
        Db.Matches.Add(game);
        await Db.SaveChangesAsync();

        var choices = (await new GamertagAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)))
            .GetChoicesAsync(Typed("Shared"), null!))!.ToList();

        Assert.That(choices.Select(c => (c.Name, c.StringValue)), Is.EqualTo(new[] { ("Shared Tag", "id:7102") }));
    }

    [Test]
    public async Task GamertagAutocomplete_ListsAnAccount_ByItsCurrentGamertag_WhenItsPastOneDiffersOnlyInSpacing()
    {
        // Its gamertag now has two spaces: what was typed isn't in it, but the past one is the same gamertag
        var respaced = XboxUser.FromXbox(7201, "Foo  Bar");
        respaced.RecordPastGamertag("Foo Bar", null, null, GamertagHistorySource.Manual);
        Db.XboxUsers.Add(respaced);
        await Db.SaveChangesAsync();

        var choices = (await new GamertagAutocomplete(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)))
            .GetChoicesAsync(Typed("Foo B"), null!))!.ToList();

        Assert.That(choices.Select(c => (c.Name, c.StringValue)), Is.EqualTo(new[] { ("Foo  Bar", "id:7201") }), "listed, unlabelled: no alias");
    }

    [Test]
    public async Task RegionAutocomplete_OffersTheRegionsNamed_AndClearingIt()
    {
        var choices = await new RegionAutocomplete(new GrifballWebApp.Application.Profile.RegionService(Db)).GetChoicesAsync(Typed("europe"), null!);

        Assert.That(Names(choices), Is.EquivalentTo(TheEuropes));
    }
}
