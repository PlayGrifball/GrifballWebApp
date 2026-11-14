using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using NetCord;
using NetCord.Rest;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// DiscordCommandsHandler's /recentmatches with games to show, past the refusals DiscordCommandsHandlerUnitTests covers:
/// on a temp-file SQLite GrifballContext with Halo matches seeded: the saved games, read through get_recent_games' method.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCommandsHandlerMatchesUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Played = new(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc);
    private DiscordCommandsHandler Handler() => new(new GrifballWebApp.Server.Commands.CommandLookups(Db, null!, new XboxAccountService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)), new HaloTools(Db, new XboxAccountService(Db)));

    /// <summary>A game the player (Xbox 42) played on team 0, ending <paramref name="outcome"/>, against a team of one (Xbox 43) or of none.</summary>
    private void AddGame(Guid id, int hoursLater, Outcomes outcome, bool opponent = true)
    {
        var game = HaloMatch.Seed(id, startTime: Played.AddHours(hoursLater), duration: TimeSpan.FromMinutes(12));
        game.AddTeam(0, 50, outcome);
        game.AddParticipant(0, 42, new MatchParticipantStats());
        if (opponent)
        {
            game.AddTeam(1, 20, outcome is Outcomes.Won ? Outcomes.Lost : Outcomes.Won);
            game.AddParticipant(1, 43, new MatchParticipantStats());
        }
        Db.Matches.Add(game);
    }

    private static List<EmbedProperties> Embeds(CommandContextRecorder discord)
        => ((IEnumerable<EmbedProperties>)discord.EditArgument(nameof(DiscordInterface.Generated.IDiscordMessageOptions.WithEmbeds))!).ToList();

    [Test]
    public async Task RecentMatches_TheCallersOwnGames_AreShownNewestFirst_ColouredByTheirOutcome()
    {
        // The caller (Discord 42) is tied to Xbox 42
        await AddUser(42, "Grifter1");
        Db.XboxUsers.Add(XboxUser.FromXbox(43, "Opponent"));
        await Db.SaveChangesAsync();
        var won = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
        var lost = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        var tied = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
        var unfinished = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
        AddGame(won, 0, Outcomes.Won);
        AddGame(lost, 1, Outcomes.Lost);
        AddGame(tied, 2, Outcomes.Tie, opponent: false);
        AddGame(unfinished, 3, Outcomes.DidNotFinish);
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().RecentMatches(discord.Context, null);

        var embeds = Embeds(discord);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(embeds.Select(e => e.Fields!.First().Value), Is.EqualTo(new[]
            {
                $"[{unfinished}](https://www.halowaypoint.com/halo-infinite/players/Grifter1/matches/{unfinished})",
                $"[{tied}](https://www.halowaypoint.com/halo-infinite/players/Grifter1/matches/{tied})",
                $"[{lost}](https://www.halowaypoint.com/halo-infinite/players/Grifter1/matches/{lost})",
                $"[{won}](https://www.halowaypoint.com/halo-infinite/players/Grifter1/matches/{won})",
            }));
            Assert.That(embeds.Select(e => e.Color), Is.EqualTo(new Color?[] { new Color(255, 255, 255), new Color(255, 255, 0), new Color(255, 0, 0), new Color(0, 255, 0) }));
        }
    }

    [Test]
    public async Task RecentMatches_AGame_ListsEachTeamsScoreOutcomeAndPlayers_SplitByARule()
    {
        await AddUser(42, "Grifter1");
        Db.XboxUsers.Add(XboxUser.FromXbox(43, "Opponent"));
        await Db.SaveChangesAsync();
        AddGame(Guid.NewGuid(), 0, Outcomes.Won);
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder(discordUserId: 42);

        await Handler().RecentMatches(discord.Context, null);

        var fields = Embeds(discord).Single().Fields!.Select(f => (f.Name, f.Value)).ToList();
        Assert.That(fields.Skip(1), Is.EqualTo(new[]
        {
            ("Start Time", $"<t:{new DateTimeOffset(Played).ToUnixTimeSeconds()}>"),
            ("Duration", "00:12:00"),
            ("Team", "0"), ("Score", "50"), ("Outcome", "Won"), ("Players", "Grifter1"),
            ("**-------------------------**", "\u200B"),
            ("Team", "1"), ("Score", "20"), ("Outcome", "Lost"), ("Players", "Opponent"),
        }));
    }
}
