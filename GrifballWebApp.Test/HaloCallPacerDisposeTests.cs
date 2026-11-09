using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Test;

/// <summary>HaloCallPacer's disposal: the stopping token's source is disposed once, and a call after it is canceled.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloCallPacerDisposeTests
{
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
}
