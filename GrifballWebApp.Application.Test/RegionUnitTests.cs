using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Application.Test;

/// <summary>RegionTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RegionUnitTests
{
    [TestCase("East", Regions.EastNorthAmerica)]
    [TestCase("Eastern", Regions.EastNorthAmerica)]
    [TestCase("NA East", Regions.EastNorthAmerica)]
    [TestCase("na-east", Regions.EastNorthAmerica)]
    [TestCase("EST", Regions.EastNorthAmerica)]
    [TestCase("Central", Regions.CentralNorthAmerica)]
    [TestCase("CST", Regions.CentralNorthAmerica)]
    [TestCase(" west ", Regions.WestNorthAmerica)]
    [TestCase("PST", Regions.WestNorthAmerica)]
    [TestCase("Europe", Regions.Europe)]
    [TestCase("EU", Regions.Europe)]
    [TestCase("Oceania", Regions.Australia)]
    public void Normalize_ReadsTheSheetsWays(string written, int region)
        => Assert.That(Regions.Normalize(written), Is.EqualTo(region));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Bot")]
    [TestCase("Mars")]
    public void Normalize_OfNothingKnown_IsNull(string? written)
        => Assert.That(Regions.Normalize(written), Is.Null);
}
