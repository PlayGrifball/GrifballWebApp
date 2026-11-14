using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Domain.Players;
using NetCord;
using NetCord.JsonModels;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagAutocompleteTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
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
            _context.XboxUsers.Add(XboxUser.FromXbox(1000 + i, $"Grunt {i:00}"));
        _context.XboxUsers.Add(XboxUser.FromXbox(2000, "Elite"));
        await _context.SaveChangesAsync();

        var choices = (await new GamertagAutocomplete(new CommandLookups(_context, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(_context))).GetChoicesAsync(Typed("runt"), null!))!.ToList();

        Assert.That(choices, Has.Count.EqualTo(25));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(choices.Select(c => c.Name), Has.All.StartWith("Grunt "));
            Assert.That(choices.Select(c => c.StringValue), Has.All.StartWith(CommandLookups.ChoicePrefix + "10"), "each is sent as its XUID");
        }
    }

    [Test]
    public async Task GetChoicesAsync_NoMatch_ReturnsNothing()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(2000, "Elite"));
        await _context.SaveChangesAsync();

        var choices = await new GamertagAutocomplete(new CommandLookups(_context, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(_context))).GetChoicesAsync(Typed("Grunt"), null!);

        Assert.That(choices, Is.Empty);
    }
}
