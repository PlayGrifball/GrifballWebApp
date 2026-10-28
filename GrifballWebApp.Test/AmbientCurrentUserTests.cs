using GrifballWebApp.Database.Services;

namespace GrifballWebApp.Test;

/// <summary>
/// How the ambient user flows between async methods. Every test is async, so what it sets is undone when it returns
/// and can't reach the next test on the same thread.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AmbientCurrentUserTests
{
    private readonly AmbientCurrentUser _ambient = new();

    private async Task SetAfterAwait(int userId)
    {
        await Task.Yield();
        _ambient.UserId = userId;
    }

    [Test]
    public async Task UserSetByAnAwaitedMethod_IsSeenByTheCaller_WhenTheCallerBegan()
    {
        _ambient.Begin();

        await SetAfterAwait(7);

        Assert.That(_ambient.UserId, Is.EqualTo(7));
        _ambient.End();
    }

    [Test]
    public void SettingTheUser_WithoutBegin_Throws()
    {
        // Why Begin exists: an AsyncLocal assigned inside an async method doesn't flow back to its caller, so a value
        // set without an enclosing slot would silently be lost. Fail loudly instead.
        Assert.Multiple(() =>
        {
            Assert.That(_ambient.IsActive, Is.False);
            Assert.That(() => _ambient.UserId = 7, Throws.InvalidOperationException);
            Assert.That(_ambient.UserId, Is.Null);
        });
    }

    [Test]
    public void IsActive_FromBeginUntilEnd()
    {
        _ambient.Begin();
        var afterBegin = _ambient.IsActive;
        _ambient.End();

        Assert.Multiple(() =>
        {
            Assert.That(afterBegin, Is.True);
            Assert.That(_ambient.IsActive, Is.False);
        });
    }

    [Test]
    public async Task End_ClearsTheUserForFlowsThatCapturedIt()
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ambient.Begin();
        _ambient.UserId = 7;
        var background = Task.Run(async () =>
        {
            await ended.Task;
            return _ambient.UserId;
        });

        _ambient.End();
        ended.SetResult();

        var seenInBackground = await background;

        Assert.Multiple(() =>
        {
            Assert.That(seenInBackground, Is.Null);
            Assert.That(_ambient.UserId, Is.Null);
        });
    }

    [Test]
    public async Task ConcurrentFlows_EachSeeTheirOwnUser()
    {
        var bothSet = new Barrier(2);
        async Task<int?> Flow(int userId)
        {
            await Task.Yield();
            _ambient.Begin();
            await SetAfterAwait(userId);
            bothSet.SignalAndWait(TimeSpan.FromSeconds(30));
            await Task.Yield();
            var seen = _ambient.UserId;
            _ambient.End();
            return seen;
        }

        var seen = await Task.WhenAll(Task.Run(() => Flow(1)), Task.Run(() => Flow(2)));

        Assert.That(seen, Is.EqualTo(new int?[] { 1, 2 }));
    }

    [Test]
    public async Task TaskStartedBeforeBegin_DoesNotSeeTheUser()
    {
        var set = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var earlier = Task.Run(async () =>
        {
            await set.Task;
            return _ambient.UserId;
        });

        _ambient.Begin();
        _ambient.UserId = 7;
        set.SetResult();

        Assert.That(await earlier, Is.Null);
        _ambient.End();
    }
}
