using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using NetCord;
using NetCord.JsonModels;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagAutocompleteTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static ApplicationCommandInteractionDataOption Typed(string value)
        => new(new JsonApplicationCommandInteractionDataOption { Name = "gamertag", Type = ApplicationCommandOptionType.String, Value = value, Focused = true });

    [Test]
    public async Task GetChoicesAsync_ReturnsUpTo25GamertagsContainingTheInput()
    {
        for (var i = 0; i < 30; i++)
            _context.XboxUsers.Add(new XboxUser { XboxUserID = 1000 + i, Gamertag = $"Grunt {i:00}" });
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 2000, Gamertag = "Elite" });
        await _context.SaveChangesAsync();

        var choices = (await new GamertagAutocomplete(_context).GetChoicesAsync(Typed("runt"), null!))!.ToList();

        Assert.That(choices, Has.Count.EqualTo(25));
        Assert.Multiple(() =>
        {
            Assert.That(choices.Select(c => c.Name), Has.All.StartWith("Grunt "));
            Assert.That(choices.Select(c => c.StringValue), Is.EqualTo(choices.Select(c => c.Name)), "the gamertag is both the label and the value");
        });
    }

    [Test]
    public async Task GetChoicesAsync_NoMatch_ReturnsNothing()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 2000, Gamertag = "Elite" });
        await _context.SaveChangesAsync();

        var choices = await new GamertagAutocomplete(_context).GetChoicesAsync(Typed("Grunt"), null!);

        Assert.That(choices, Is.Empty);
    }
}
