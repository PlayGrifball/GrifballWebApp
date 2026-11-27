using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Application.Test;

/// <summary>The slash commands a chat can look up (<see cref="SlashCommandTools"/>), so it never guesses at them.</summary>
[TestFixture]
internal sealed class SlashCommandToolsUnitTests
{
    private sealed class Catalog(params SlashCommandDto[] commands) : ISlashCommandCatalog
    {
        public IReadOnlyList<SlashCommandDto> Commands() => commands;
    }

    private static readonly SlashCommandDto[] Registered =
    [
        new("/season info", "A season's league, dates, teams and champions"),
        new("/season standings", "A season's team standings"),
        new("/setgamertag", "Set your gamertag"),
    ];

    private static readonly string[] SeasonParts = ["/season info", "/season standings"];
    private static readonly string[] SetGamertagOnly = ["/setgamertag"];

    [Test]
    public void ListSlashCommands_GivesEveryCommand_OrOnlyTheNamedOnesParts()
    {
        var tools = new SlashCommandTools(new Catalog(Registered));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.ListSlashCommands(), Is.EqualTo(Registered));
            Assert.That(tools.ListSlashCommands("season").Select(c => c.Command), Is.EqualTo(SeasonParts));
            Assert.That(tools.ListSlashCommands("/SetGamertag").Select(c => c.Command), Is.EqualTo(SetGamertagOnly));
        }
    }

    [Test]
    public void ListSlashCommands_RefusesAnUnknownCommand_AndAHostWithoutTheBot()
    {
        var unknown = Assert.Throws<ToolException>(() => new SlashCommandTools(new Catalog(Registered)).ListSlashCommands("nope"));
        var noBot = Assert.Throws<ToolException>(() => new SlashCommandTools(new NoSlashCommands()).ListSlashCommands());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unknown.Message, Does.Contain("no /nope command"));
            Assert.That(noBot.Message, Does.Contain("isn't running here"));
        }
    }

    [Test]
    public void TheChatsShortList_HasTheCommandLookup_ForAnyone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifTools.Chat, Does.Contain("list_slash_commands"));
            Assert.That(GrifTools.RolesByName["list_slash_commands"], Is.Empty);
            Assert.That(GrifTools.EffectByName["list_slash_commands"], Is.EqualTo(ToolEffect.ReadsOnly));
        }
    }
}
