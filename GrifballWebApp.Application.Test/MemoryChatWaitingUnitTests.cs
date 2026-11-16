using GrifballWebApp.Application.Chat;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>One process's waiting changes (<see cref="MemoryChatWaiting"/>): kept until taken, forgotten or expired; taken once.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MemoryChatWaitingUnitTests
{
    private static readonly ChatAction Action = new("a1", "ollama", "run_job()", []);
    private readonly FakeTimeProvider _time = new();

    [Test]
    public async Task Peek_LeavesItWaiting_Take_GetsItOnce()
    {
        var waiting = new MemoryChatWaiting(_time);
        await waiting.SaveAsync("k", Action, TimeSpan.FromMinutes(10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await waiting.PeekAsync("k"), Is.EqualTo(Action));
            Assert.That(await waiting.PeekAsync("k"), Is.EqualTo(Action));
            Assert.That(await waiting.TakeAsync("k"), Is.EqualTo(Action));
            Assert.That(await waiting.TakeAsync("k"), Is.Null, "Taken");
            Assert.That(await waiting.PeekAsync("other"), Is.Null);
        }
    }

    [Test]
    public async Task ManyTakesAtOnce_OneGetsIt()
    {
        var waiting = new MemoryChatWaiting(_time);
        await waiting.SaveAsync("k", Action, TimeSpan.FromMinutes(10));

        var taken = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => waiting.TakeAsync("k"))));

        Assert.That(taken.Count(a => a is not null), Is.EqualTo(1));
    }

    [Test]
    public async Task Expired_OrForgotten_IsGone()
    {
        var waiting = new MemoryChatWaiting(_time);
        await waiting.SaveAsync("expires", Action, TimeSpan.FromMinutes(10));
        await waiting.SaveAsync("forgotten", Action, TimeSpan.FromMinutes(10));

        await waiting.ForgetAsync("forgotten");
        _time.Advance(TimeSpan.FromMinutes(9));
        var stillThere = await waiting.PeekAsync("expires");
        _time.Advance(TimeSpan.FromMinutes(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await waiting.PeekAsync("forgotten"), Is.Null);
            Assert.That(stillThere, Is.EqualTo(Action));
            Assert.That(await waiting.TakeAsync("expires"), Is.Null);
        }
    }
}
