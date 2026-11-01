using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>A gamertag an Xbox account's history has for the season, when no site user holds that account.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PlayerResolverHistoryTests
{
    private GrifballContext _context = null!;
    private Season _season = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _season = await _context.AddSeason("History Season");
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task XboxAccountThatHad(long xuid, string current, string earlier)
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = xuid, Gamertag = current });
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory
        {
            XboxUserID = xuid, Gamertag = earlier, NormalizedGamertag = HistoricalKeys.Normalize(earlier), Source = GamertagHistorySource.HistoricalImport,
        });
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task TheGamertagsPlaceholder_IsLinkedToTheAccount()
    {
        var placeholder = new User { UserName = HistoricalKeys.DummyUserName("Old Tag"), DisplayName = "Old Tag", IsDummyUser = true };
        _context.Users.Add(placeholder);
        await XboxAccountThatHad(55, "New Tag", "Old Tag");

        var resolution = await new PlayerResolver(_context, _season.SeasonID, _season.SeasonStart).Resolve("Old Tag", null, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(resolution, Is.EqualTo(new Resolution(placeholder.Id, ResolvedBy.GamertagHistory)));
            Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(55));
        });
    }

    [Test]
    public async Task WithoutAPlaceholder_OneIsMade_ForTheAccount()
    {
        await XboxAccountThatHad(56, "Newer Tag", "Older Tag");

        var resolution = await new PlayerResolver(_context, _season.SeasonID, _season.SeasonStart).Resolve("Older Tag", null, CancellationToken.None);

        var made = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == resolution.UserId);
        Assert.Multiple(() =>
        {
            Assert.That(resolution.By, Is.EqualTo(ResolvedBy.GamertagHistory));
            Assert.That((made.IsDummyUser, made.XboxUserID), Is.EqualTo((true, (long?)56)));
        });
    }
}
