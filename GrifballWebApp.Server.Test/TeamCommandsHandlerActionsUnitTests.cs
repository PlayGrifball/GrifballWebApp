using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// TeamCommandsHandler with something to show, past the lookups TeamCommandsHandlerUnitTests covers: a season with a
/// team's roster, match and VOD, on a temp-file SQLite GrifballContext with the real team page services.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamCommandsHandlerActionsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    private TeamCommandsHandler Handler() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)), new TeamPageService(Db, _time), new TeamHistoryService(Db, new GrifballWebApp.Application.Seasons.SeasonVisibility(Db, _time), _time),
        new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!), Urls());

    private async Task<(Season Season, Team Home, Team Away, SeasonMatch Match)> ASeason()
    {
        var season = Season.Seed(seasonName: "Winter").WithDates(signupsOpen: Now.AddDays(-60), signupsClose: Now.AddDays(-50),
            draftStart: Now.AddDays(-40), seasonStart: Now.AddDays(-30), seasonEnd: Now.AddDays(30));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var home = await AddTeam(season, "Grifs");
        var away = await AddTeam(season, "Rivals");
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, scheduledTime: Now.AddDays(2));
        Db.SeasonMatches.Add(match);
        await Db.SaveChangesAsync();
        return (season, home, away, match);
    }

    [Test]
    public async Task Roster_ListsThePlayers_WithTheirDraftRound()
    {
        var (_, home, _, _) = await ASeason();
        var first = await AddUser(5, "Grifter1");
        var second = await AddUser(6, "Grifter2");
        Db.Set<TeamPlayer>().AddRange(TeamPlayer.Seed(teamID: home.TeamID, userID: second.Id, draftRound: 2), TeamPlayer.Seed(teamID: home.TeamID, userID: first.Id, draftRound: 1));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Roster(discord.Context, "Grifs");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Grifs roster"));
            Assert.That(embed.Description, Is.EqualTo(
                $"[Grifter1](https://grif.test/profile/{first.Id}) · round 1\n[Grifter2](https://grif.test/profile/{second.Id}) · round 2"));
        }
    }

    [Test]
    public async Task History_IsTheTeamsSeasons_WithItsRecord()
    {
        var (season, home, _, _) = await ASeason();
        var discord = new CommandContextRecorder();

        await Handler().History(discord.Context, "Grifs");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((embed.Title, embed.Url), Is.EqualTo(("Grifs history", $"https://grif.test/history/team/{home.TeamID}")));
            Assert.That(embed.Description, Does.Contain($"[Winter](https://grif.test/season/{season.SeasonID}/team/{home.TeamID}) · 0-0"));
        }
    }

    [Test]
    public async Task Matches_ListsTheTeamsMatches_WithTheirTimeAndOpponent()
    {
        var (_, _, _, match) = await ASeason();
        var discord = new CommandContextRecorder();

        await Handler().Matches(discord.Context, "Grifs");

        Assert.That(discord.Embed().Description, Is.EqualTo(
            $"<t:{new DateTimeOffset(Now.AddDays(2)).ToUnixTimeSeconds()}> · [vs Rivals](https://grif.test/seasonmatch/{match.SeasonMatchID})"));
    }

    [Test]
    public async Task Vods_ListsTheTeamsVods()
    {
        var (season, home, away, match) = await ASeason();
        var published = Now.AddDays(-3);
        Db.Vods.Add(Vod.Seed("abc123", "Grifs vs Rivals", published, VodMatchStatus.Confirmed, season.SeasonID, match.SeasonMatchID, [home.TeamID, away.TeamID]));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Vods(discord.Context, "Grifs");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Grifs VODs"));
            Assert.That(embed.Description, Is.EqualTo(
                $"[Grifs vs Rivals](https://www.youtube.com/watch?v=abc123) · <t:{new DateTimeOffset(published).ToUnixTimeSeconds()}>"));
        }
    }
}
