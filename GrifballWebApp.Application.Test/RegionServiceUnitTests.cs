using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>RegionService against the fake IApplicationDbContext: the regions (RegionConfiguration seeds them through
/// HasData) and a player choosing theirs.</summary>
[TestFixture]
internal sealed class RegionServiceUnitTests : ApplicationDbContextTests
{
    private const int WestNorthAmericaRegionID = 1;
    private const int EuropeRegionID = 7;
    private static readonly int[] SeededRegionIDs = [1, 2, 3, 4, 5, 6, 7];

    private RegionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new RegionService(Context);
    }

    private async Task<User> ReadUser(int id)
    {
        Context.ChangeTracker.Clear();
        return await Context.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Test]
    public async Task List_ReturnsEveryRegion_ByID()
    {
        var regions = await _service.List();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(regions.Select(r => r.RegionID), Is.EqualTo(SeededRegionIDs));
            Assert.That(regions[0].RegionName, Is.EqualTo("West North America"));
            Assert.That(regions[^1].RegionName, Is.EqualTo("Europe"));
        }
    }

    [Test]
    public async Task Set_AKnownRegion_SetsItAsTheUsersChoice()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();

        var result = await _service.Set(1, EuropeRegionID);

        var user = await ReadUser(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SetRegionResult.Set));
            Assert.That(user.RegionID, Is.EqualTo(EuropeRegionID));
            Assert.That(user.RegionSource, Is.EqualTo(RegionSource.User));
        }
    }

    [Test]
    public async Task Set_Null_ClearsTheRegionAndItsSource()
    {
        Context.Users.Add(User.Seed(userName: "alice", regionID: WestNorthAmericaRegionID, regionSource: RegionSource.Sysadmin, id: 1));
        await Context.SaveChangesAsync();

        var result = await _service.Set(1, null);

        var user = await ReadUser(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SetRegionResult.Set));
            Assert.That(user.RegionID, Is.Null);
            Assert.That(user.RegionSource, Is.Null);
        }
    }

    [Test]
    public async Task Set_AnUnknownRegion_IsRefusedAndChangesNothing()
    {
        Context.Users.Add(User.Seed(userName: "alice", regionID: WestNorthAmericaRegionID, regionSource: RegionSource.User, id: 1));
        await Context.SaveChangesAsync();

        var result = await _service.Set(1, 999);

        var user = await ReadUser(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SetRegionResult.NoSuchRegion));
            Assert.That(user.RegionID, Is.EqualTo(WestNorthAmericaRegionID));
        }
    }

    [TestCase(WestNorthAmericaRegionID)]
    [TestCase(null)]
    public async Task Set_NoSuchUser_ReturnsNoSuchUser(int? regionID)
    {
        var result = await _service.Set(999, regionID);

        Assert.That(result, Is.EqualTo(SetRegionResult.NoSuchUser));
    }
}
