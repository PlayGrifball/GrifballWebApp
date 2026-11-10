using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Test;

/// <summary>HaloCallPacer's disposal: the stopping token's source is disposed once, and a call after it is canceled.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloCallPacerDisposeTests
{
    /// <summary>Shared turns whose reservation disposes the pacer (the app stopping mid-call), then hands out a wait.</summary>
    private sealed class DisposingSlots : IHaloCallSlots
    {
        public HaloCallPacer? Pacer { get; set; }

        public async Task<TimeSpan> ReserveAsync(TimeSpan perCall, CancellationToken ct)
        {
            await Pacer!.DisposeAsync();
            return TimeSpan.FromMinutes(1);
        }
    }

    [Test]
    public async Task DisposingTwice_IsANoOp_AndAWaitAfterwards_IsCanceled()
    {
        var pacer = new HaloCallPacer(60);

        await pacer.DisposeAsync();

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThatAsync(async () => await pacer.DisposeAsync(), Throws.Nothing);
            await Assert.ThatAsync(async () => await pacer.WaitAsync(), Throws.InstanceOf<OperationCanceledException>());
        }
    }

    [Test]
    public async Task DisposedWhileAWaitIsRunning_TheWaitIsCanceled_NotObjectDisposed()
    {
        var slots = new DisposingSlots();
        var pacer = new HaloCallPacer(60, slots);
        slots.Pacer = pacer;

        await Assert.ThatAsync(async () => await pacer.WaitAsync(), Throws.InstanceOf<OperationCanceledException>().And.Not.InstanceOf<ObjectDisposedException>());
    }
}
