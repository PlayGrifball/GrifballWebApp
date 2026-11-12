using GrifballWebApp.Application;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Integration.Test;

/// <summary>/help (<see cref="HelpCommandsHandler"/>), over the server's real modules as Program.cs registers them.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HelpCommandsTests
{
    private static readonly string[] SomeCommands = ["/help", "/matches", "/season standings", "/admin match report", "/reschedule decide"];

    private static HelpCommandsHandler Handler()
    {
        var service = new ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext>();
        service.AddModules(typeof(GrifballWebApp.Server.Program).Assembly);
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        return new HelpCommandsHandler(service, urls);
    }

    [Test]
    public void Commands_Should_ListCommandsAndEverySubcommand_NestedGroupsToo()
    {
        var paths = Handler().Commands().Select(x => x.Path).ToList();

        Assert.That(paths, Is.SupersetOf(SomeCommands));
    }

    [Test]
    public async Task Help_Should_ListEachCommandOnce_Privately()
    {
        var discord = new CommandContextRecorder();

        await Handler().Help(discord.Context, null);

        var description = discord.Embed().Description;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.False);
            Assert.That(description, Does.Contain("`/help` · The bot's commands"));
            Assert.That(description, Does.Contain("`/season` · bracket, draft, grades, info, list, schedule, signups, standings, stats"));
            Assert.That(description, Does.Contain("`/admin` · bracket create, bracket seed,"));
        }
    }

    [TestCase("admin", "`/admin match report` · Report a Halo game as one of the match's")]
    [TestCase("/queue", "`/queue join` · Join the queue")]
    [TestCase("nothing", "There is no /nothing command.")]
    public async Task Help_Should_ListACommandsParts(string command, string expected)
    {
        var discord = new CommandContextRecorder();

        await Handler().Help(discord.Context, command);

        Assert.That(discord.Embed().Description, Does.Contain(expected));
    }
}
