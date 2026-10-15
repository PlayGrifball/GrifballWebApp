using DiscordInterface.Generated;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Preview;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class PreviewModeTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void IsEnabled_Should_BeFalse_When_KeyIsUnset()
    {
        Assert.That(PreviewMode.IsEnabled(Configuration([])), Is.False);
    }

    [TestCase("false")]
    [TestCase("False")]
    public void IsEnabled_Should_BeFalse_When_KeyIsFalse(string value)
    {
        Assert.That(PreviewMode.IsEnabled(Configuration(new() { ["Preview:Enabled"] = value })), Is.False);
    }

    [TestCase("true")]
    [TestCase("True")]
    public void IsEnabled_Should_BeTrue_When_KeyIsTrue(string value)
    {
        Assert.That(PreviewMode.IsEnabled(Configuration(new() { ["Preview:Enabled"] = value })), Is.True);
    }

    [Test]
    public void IsEnabled_Should_ReadTheEnvironmentVariableForm()
    {
        const string name = "Preview__Enabled";
        var previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "true");
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            Assert.That(PreviewMode.IsEnabled(configuration), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    private static ServiceProvider PreviewServices(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddPreviewDiscord(Configuration(values));
        services.AddSingleton<IDiscordRestClient, DiscordRestClient>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    [Test]
    public void AddPreviewDiscord_Should_NotRequireDiscordSettings()
    {
        using var provider = PreviewServices([]);

        var options = provider.GetRequiredService<IOptions<DiscordOptions>>().Value;

        Assert.That(options.Token, Is.Null);
        Assert.That(options.DisableGlobally, Is.True);
    }

    [Test]
    public void AddPreviewDiscord_Should_ForceDisableGlobally_When_ConfigSaysOtherwise()
    {
        using var provider = PreviewServices(new() { ["Discord:DisableGlobally"] = "false", ["Discord:DraftChannel"] = "123" });

        var options = provider.GetRequiredService<IOptions<DiscordOptions>>().Value;

        Assert.That(options.DisableGlobally, Is.True);
        Assert.That(options.DraftChannel, Is.EqualTo(123UL));
    }

    [Test]
    public void AddPreviewDiscord_Should_FailDiscordRequestsLocally()
    {
        using var provider = PreviewServices([]);

        Assert.That(provider.GetRequiredService<RestClient>().Token, Is.Null);

        var client = provider.GetRequiredService<IDiscordRestClient>();

        var ex = Assert.ThrowsAsync<PreviewDiscordDisabledException>(()
            => client.SendMessageAsync(1234, new MessageProperties { Content = "hello" }));
        Assert.That(ex!.Message, Does.StartWith("Discord is disabled in preview mode"));
    }
}
