using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Domain.Players.User;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>GetsertXboxUserServiceErrorTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GetsertXboxUserServiceErrorUnitTests
{
    [Test]
    public void AHaloError_IsKeptShort()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifballWebApp.Infrastructure.HaloInfinite.HaloErrors.Describe(new string('x', 1000)), Has.Length.EqualTo(GrifballWebApp.Infrastructure.HaloInfinite.HaloErrors.MaxLength + 1));
            Assert.That(GrifballWebApp.Infrastructure.HaloInfinite.HaloErrors.Describe(null), Is.EqualTo("no error message"));
            Assert.That(GrifballWebApp.Infrastructure.HaloInfinite.HaloErrors.Describe("<html><body>no title</body></html>"), Is.EqualTo("an HTML error page (no title)"));
        }
    }
}
