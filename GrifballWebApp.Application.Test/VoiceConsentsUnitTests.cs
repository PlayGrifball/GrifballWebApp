using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Voice;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>Who the voice chat may hear (<see cref="VoiceConsents"/>): only the Discord accounts that opted in.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class VoiceConsentsUnitTests
{
    private FakeApplicationDbContext _db = null!;

    [SetUp]
    public void SetUp() => _db = new FakeApplicationDbContext();

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task OptIn_AnAccountTheBotHasntSeen_IsAddedOptedIn_AndOptOutUndoesIt()
    {
        var consents = new VoiceConsents(_db);

        await consents.OptInAsync(42, "grunt");
        var heard = await consents.OptedInAsync([42, 43]);
        await consents.OptOutAsync(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(heard, Is.EquivalentTo(new ulong[] { 42 }), "43 never opted in");
            Assert.That(await consents.OptedInAsync([42]), Is.Empty);
            Assert.That(await _db.DiscordUsers.AsNoTracking().Select(d => d.DiscordUsername).SingleAsync(), Is.EqualTo("grunt"));
        }
    }

    [Test]
    public async Task OptIn_AnAccountTheBotKnows_KeepsIt_AndOptingOutWithoutOneDoesNothing()
    {
        _db.DiscordUsers.Add(DiscordUser.FromDiscord(42, "grunt"));
        await _db.SaveChangesAsync();
        var consents = new VoiceConsents(_db);

        await consents.OptInAsync(42, "renamed");
        await consents.OptOutAsync(99);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await consents.OptedInAsync([42]), Is.EquivalentTo(new ulong[] { 42 }));
            Assert.That(await _db.DiscordUsers.CountAsync(), Is.EqualTo(1), "No account made for an opt-out");
        }
    }
}
