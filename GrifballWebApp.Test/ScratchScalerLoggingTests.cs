using System.Net;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>KubernetesScratchScaler logs the scratch server coming up and being stopped, only when Information is on.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ScratchScalerLoggingTests
{
    /// <summary>The Kubernetes API: every call answered with a Deployment that has a ready replica.</summary>
    private sealed class ReadyApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"status":{"readyReplicas":1}}""") });
    }

    [TestCase(true, 2)]
    [TestCase(false, 0)]
    public async Task StartAndStop_AreLogged_OnlyWhenInformationIsOn(bool informationOn, int logged)
    {
        var logger = Substitute.For<ILogger<KubernetesScratchScaler>>();
        logger.IsEnabled(LogLevel.Information).Returns(informationOn);
        using var http = new HttpClient(new ReadyApi()) { BaseAddress = new Uri("https://api.cluster:6443") };
        var scaler = new KubernetesScratchScaler(http, "grif-x", "grif-backup-scratch", () => "token", logger);

        await scaler.StartAsync(CancellationToken.None);
        await scaler.StopAsync(CancellationToken.None);

        Assert.That(logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log)), Is.EqualTo(logged), "the start and the stop, when on");
    }
}
