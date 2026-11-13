using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;
using GrifballWebApp.Infrastructure.HaloInfinite;
using static GrifballWebApp.Server.Test.HaloInfiniteHealthCheckTestData;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// How the Server registers HaloInfiniteHealthCheck (AddAppHealthChecks). The check's own behavior (the probe, its
/// caching, FromConfiguration) is the Infrastructure layer's: GrifballWebApp.Infrastructure.Test's
/// HaloInfiniteHealthCheckTests.
/// </summary>
[TestFixture]
public class HaloInfiniteHealthCheckTests
{
    [Test]
    public void AddAppHealthChecks_RegistersHaloAsDegradedOnly_AndOneSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHaloInfiniteClient>(Answering(200));
        services.AddAppHealthChecks(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single(r => r.Name == "halo_infinite");
        var first = registration.Factory(provider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registration.FailureStatus, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(registration.Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registration.Factory(provider), Is.SameAs(first), "the cache lives in one instance");
            Assert.That(provider.GetRequiredService<HaloInfiniteHealthCheck>().Interval, Is.EqualTo(HaloInfiniteHealthCheck.DefaultInterval));
        }
    }
}
