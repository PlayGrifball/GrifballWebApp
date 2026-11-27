using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
internal sealed class ButtonToolsUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ToolCaller Staff = new(1, [SiteRoles.Commissioner]);
    private readonly FakeTimeProvider _time = new(Now);

    private sealed class Bot : ISlashCommandCatalog
    {
        public IReadOnlyList<SlashCommandDto> Commands() => [new("/help", "Help")];
    }

    private ButtonTools Tools(ToolCaller? caller = null, ISlashCommandCatalog? catalog = null)
        => new(caller ?? ToolCaller.Anonymous, catalog ?? new Bot(), new SeasonVisibility(Context, _time), Context, _time);

    /// <summary>A season whose signups opened <paramref name="signupsOpenDaysAgo"/> days ago, for five days; announced or not.</summary>
    private async Task<int> SeasonAsync(int signupsOpenDaysAgo, bool announced)
    {
        var season = Season.Seed(seasonName: "Winter");
        var open = Now.AddDays(-signupsOpenDaysAgo);
        season.Schedule(signupsOpen: open, signupsClose: open.AddDays(5), draftStart: open.AddDays(10), seasonStart: open.AddDays(20), seasonEnd: open.AddDays(60));
        if (announced)
            season.AnnounceAt(open.AddDays(-1));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season.SeasonID;
    }

    [Test]
    public async Task OfferButton_GivesTheToken_InItsResult()
    {
        var season = await SeasonAsync(signupsOpenDaysAgo: 1, announced: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatButtons.In(await Tools().OfferButton(ChatButtonKind.Signup, season)), Is.EqualTo(new[] { new ChatButton(ChatButtonKind.Signup, season) }));
            Assert.That(ChatButtons.In(await Tools().OfferButton(ChatButtonKind.JoinQueue)), Is.EqualTo(new[] { new ChatButton(ChatButtonKind.JoinQueue, null) }));
        }
    }

    [Test]
    public void OfferButton_RefusesAMissingOrNeedlessSeason_AndAHostWithoutTheBot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.ThrowsAsync<ToolException>(() => Tools().OfferButton(ChatButtonKind.Signup));
            Assert.ThrowsAsync<ToolException>(() => Tools().OfferButton(ChatButtonKind.JoinQueue, 5));
            Assert.ThrowsAsync<ToolException>(() => Tools(catalog: new NoSlashCommands()).OfferButton(ChatButtonKind.JoinQueue));
        }
    }

    [Test]
    public async Task OfferButton_ASeasonTheCallerCantSee_IsNoSuchSeason_AStaffersClosedOneHasNoSignups()
    {
        var unannounced = await SeasonAsync(signupsOpenDaysAgo: 1, announced: false);
        var closed = await SeasonAsync(signupsOpenDaysAgo: 8, announced: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Assert.ThrowsAsync<ToolException>(() => Tools().OfferButton(ChatButtonKind.SignupDetails, 999))!.Message, Is.EqualTo(ButtonTools.NoSuchSeason));
            Assert.That(Assert.ThrowsAsync<ToolException>(() => Tools().OfferButton(ChatButtonKind.Signup, unannounced))!.Message, Is.EqualTo(ButtonTools.NoSuchSeason),
                "an unannounced season stays out of a public reply");
            Assert.That(Assert.ThrowsAsync<ToolException>(() => Tools(Staff).OfferButton(ChatButtonKind.Signup, closed))!.Message, Is.EqualTo(ButtonTools.SignupsNotOpen));
            Assert.That(ChatButtons.In(await Tools(Staff).OfferButton(ChatButtonKind.Signup, unannounced)), Has.Count.EqualTo(1), "staff see it");
        }
    }
}
