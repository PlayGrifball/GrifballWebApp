using GrifballWebApp.Historical.Sources;

namespace GrifballWebApp.Historical.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class HistoricalSourceFetcherUnitTests
{
    [Test]
    public void Configure_GivesTheClientTheRefreshsUserAgentTimeoutAndBuffer()
    {
        using var client = new HttpClient();

        HistoricalSourceFetcher.Configure(client);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(client.DefaultRequestHeaders.UserAgent.ToString(), Is.EqualTo("GrifballWebApp/1.0 (historical archive)"));
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(client.MaxResponseContentBufferSize, Is.EqualTo(64 * 1024 * 1024));
        }
    }
}
