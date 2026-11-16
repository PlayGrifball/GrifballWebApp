using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Voice;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>Who the voice chat may hear (<see cref="VoiceConsents"/>): only the Discord accounts that opted in.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class VoiceConsentsUnitTests
{
    private static readonly ulong[] OnlyFortyTwo = [42];
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private FakeApplicationDbContext _db = null!;

    [SetUp]
    public void SetUp() => _db = new FakeApplicationDbContext();

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task OptIn_KeepsTheirConsent_OnceThoughGivenTwice_AndOptOutRemovesIt()
    {
        var consents = new VoiceConsents(_db, _time);

        await consents.OptInAsync(42);
        await consents.OptInAsync(42);
        var heard = await consents.OptedInAsync([42, 43]);
        var given = await _db.VoiceConsents.AsNoTracking().Select(v => v.GivenAt).SingleAsync();
        await consents.OptOutAsync(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(heard, Is.EquivalentTo(OnlyFortyTwo), "43 never opted in");
            Assert.That(given, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
            Assert.That(await consents.OptedInAsync([42]), Is.Empty);
        }
    }

    [Test]
    public async Task OptOut_WithNoConsent_DoesNothing()
    {
        await new VoiceConsents(_db, _time).OptOutAsync(99);

        Assert.That(await _db.VoiceConsents.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task OptIn_TwiceAtOnce_StandsOnce_WithoutAnError()
    {
        // Another opt-in for the same account saves between this one's check and its save
        await using var racing = _db.NewContextLike(new AnotherOptInFirst(_db, _time));

        await new VoiceConsents(racing, _time).OptInAsync(42);

        Assert.That(await _db.VoiceConsents.CountAsync(), Is.EqualTo(1));
    }

    /// <summary>Saves the same consent through another context just before this one saves: a second /voice optin at once.</summary>
    private sealed class AnotherOptInFirst(FakeApplicationDbContext db, TimeProvider time) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = db.NewContextLike();
                other.VoiceConsents.Add(GrifballWebApp.Domain.Players.VoiceConsent.Give(42, time.GetUtcNow().UtcDateTime));
                await other.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }
}
