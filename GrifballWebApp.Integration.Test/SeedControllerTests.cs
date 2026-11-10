using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Server.Controllers;
using NSubstitute;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
public class SeedControllerTests
{
    [Test]
    public async Task Medals_DownloadsTheMedals()
    {
        var dataPull = Substitute.For<IDataPullService>();

        await new SeedController(dataPull).Medals();

        await dataPull.Received(1).DownloadMedals();
    }
}
