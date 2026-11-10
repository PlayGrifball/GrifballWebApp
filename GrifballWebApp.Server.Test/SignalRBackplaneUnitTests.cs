using GrifballWebApp.Server.SignalR;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static GrifballWebApp.Server.Test.SignalRBackplaneTestData;

namespace GrifballWebApp.Server.Test;

/// <summary>SignalRBackplaneTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[NonParallelizable]
public class SignalRBackplaneUnitTests
{
    [Test]
    public void WithoutAConnectionString_EachProcessKeepsItsOwnConnections()
    {
        using var services = Services(new() { ["ConnectionStrings:Redis"] = " " });

        Assert.That(LifetimeManager(services), Is.EqualTo(typeof(DefaultHubLifetimeManager<>)));
    }

    [Test]
    public void TheConnectionStringsOwnAbortConnect_AndAChannelPrefix_AreKept()
    {
        using var services = Services(new()
        {
            ["ConnectionStrings:Redis"] = "127.0.0.1:9,abortConnect=true",
            ["SignalR:RedisChannelPrefix"] = "grif-test",
        });

        var options = services.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.AbortOnConnectFail, Is.True);
            Assert.That(options.ChannelPrefix.ToString(), Is.EqualTo("grif-test"));
        }
    }
}

/// <summary>SignalRBackplaneTests' helpers, shared with its integration tests.</summary>
internal static class SignalRBackplaneTestData
{
    internal static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSignalR().AddAppBackplane(configuration);
        return services.BuildServiceProvider();
    }

    internal static Type? LifetimeManager(ServiceProvider services)
        => services.GetRequiredService<HubLifetimeManager<TeamsHub>>().GetType().GetGenericTypeDefinition();
}
