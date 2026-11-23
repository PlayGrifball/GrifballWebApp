using GrifballWebApp.Server.Live;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// LiveOptions (the <c>Live</c> configuration section) and AddValidatedLiveOptions: binds with the shipped defaults
/// when nothing is set, binds every setting when all are, and rejects a non-positive limit or duration, or a build
/// lock that doesn't outlive the wait for it.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LiveOptionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IOptions<LiveOptions> Resolve(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddValidatedLiveOptions(Config(values));
        return services.BuildServiceProvider().GetRequiredService<IOptions<LiveOptions>>();
    }

    [Test]
    public void AddValidatedLiveOptions_NoSettings_ResolvesTheShippedDefaults()
    {
        var options = Resolve([]).Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.MaxConcurrentBuilds, Is.EqualTo(4));
            Assert.That(options.DataTtl, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(options.BuildWait, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(options.LockTtl, Is.EqualTo(TimeSpan.FromSeconds(13)));
            Assert.That(options.SharedBuilds, Is.True);
        }
    }

    [Test]
    public void AddValidatedLiveOptions_EverySettingPresent_BindsEachOne()
    {
        var options = Resolve(new()
        {
            ["Live:MaxConcurrentBuilds"] = "8",
            ["Live:DataTtl"] = "00:00:10",
            ["Live:BuildWait"] = "00:00:02",
            ["Live:LockTtl"] = "00:00:20",
            ["Live:SharedBuilds"] = "false",
        }).Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.MaxConcurrentBuilds, Is.EqualTo(8));
            Assert.That(options.DataTtl, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(options.BuildWait, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(options.LockTtl, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(options.SharedBuilds, Is.False);
        }
    }

    [TestCase("Live:MaxConcurrentBuilds", "0", "MaxConcurrentBuilds must be at least 1")]
    [TestCase("Live:DataTtl", "00:00:00", "DataTtl must be positive")]
    [TestCase("Live:BuildWait", "00:00:00", "BuildWait must be positive")]
    [TestCase("Live:LockTtl", "00:00:00", "LockTtl must be positive")]
    public void AddValidatedLiveOptions_ANonPositiveSetting_Throws(string key, string value, string expectedError)
    {
        // The validator throws ArgumentException itself, rather than returning false (which the options framework
        // would wrap in OptionsValidationException)
        var options = Resolve(new() { [key] = value });

        var ex = Assert.Throws<ArgumentException>(() => _ = options.Value);

        Assert.That(ex!.Message, Does.Contain(expectedError));
    }

    [Test]
    public void AddValidatedLiveOptions_LockTtlNotPastBuildWait_Throws()
    {
        // Equal, not just less: a lock exactly as long as the wait is still a late pod's freshly-expired lock
        var options = Resolve(new() { ["Live:BuildWait"] = "00:00:10", ["Live:LockTtl"] = "00:00:10" });

        var ex = Assert.Throws<ArgumentException>(() => _ = options.Value);

        Assert.That(ex!.Message, Does.Contain("LockTtl must be greater than BuildWait"));
    }
}
