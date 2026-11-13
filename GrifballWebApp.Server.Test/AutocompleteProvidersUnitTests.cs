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

        var choices = await new SeasonAutocomplete(new CommandLookups(Db, null!)).GetChoicesAsync(Typed("Win"), null!);

        Assert.That(Names(choices), Is.EqualTo(WinterOnly));
    }

    [Test]
    public async Task TeamAutocomplete_OffersTheTeamsNamed_WithTheirSeason()
    {
        await SeedLeague();

        var choices = await new TeamAutocomplete(new CommandLookups(Db, null!)).GetChoicesAsync(Typed("Gri"), null!);

        Assert.That(Names(choices), Is.EqualTo(GrifsInWinter));
    }

    [Test]
    public async Task PlayerAutocomplete_OffersThePlayersWhoseGamertagHoldsIt()
    {
        await SeedLeague();

        var choices = await new PlayerAutocomplete(new CommandLookups(Db, null!)).GetChoicesAsync(Typed("Grif"), null!);

        Assert.That(Names(choices), Is.EqualTo(HoldingGrif));
    }

    [Test]
    public async Task SeasonMatchAutocomplete_OffersTheMatchesOfTheTeamsNamed()
    {
        await SeedLeague();

        var choices = await new SeasonMatchAutocomplete(new CommandLookups(Db, null!)).GetChoicesAsync(Typed("Rivals"), null!);

        Assert.That(Names(choices), Is.EqualTo(TheMatchInWinter));
    }

    [Test]
    public async Task GamertagAutocomplete_OffersTheGamertagsHoldingWhatIsTyped()
    {
        await SeedLeague();

        var choices = await new GamertagAutocomplete(Db).GetChoicesAsync(Typed("Grif"), null!);

        Assert.That(Names(choices), Is.EquivalentTo(HoldingGrif));
    }
}
