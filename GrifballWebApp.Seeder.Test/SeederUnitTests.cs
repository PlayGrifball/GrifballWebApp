using GrifballWebApp.Seeder;
using Microsoft.Extensions.Configuration;
using static GrifballWebApp.Seeder.Test.SeederTestData;

namespace GrifballWebApp.Seeder.Test;

/// <summary>SeederTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeederUnitTests
{
    [Test]
    public async Task FileReader_MissingFolderConfig_Throws()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => new FileReader(Config(null)).LoadIcon("Gold1.png"));

        Assert.That(ex!.Message, Is.EqualTo("Missing Folder in configuration cannot load icon"));
    }

    [Test]
    public async Task TestReader_LoadIcon_AlwaysReturnsTheSameDummyImage()
    {
        var first = await new TestReader().LoadIcon("Gold1.png");
        var second = await new TestReader().LoadIcon("Iron2.png");

        Assert.That(first, Is.EqualTo(second), "TestReader ignores the requested path: every icon is the same dummy image");
        Assert.That(first, Has.Length.GreaterThan(0));
    }
}

/// <summary>SeederTests' helpers, shared with its integration tests.</summary>
internal static class SeederTestData
{
    internal static IConfiguration Config(string? folder) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Folder"] = folder }).Build();
}
