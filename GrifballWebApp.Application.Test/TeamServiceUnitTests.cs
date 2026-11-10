
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Teams;

namespace GrifballWebApp.Application.Test;

/// <summary>TeamServiceTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamServiceUnitTests
{
    private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Test]
    public void RemoveCaptainDto_Should_IgnoreUnknownOrderNumber_When_BoundFromWebJson()
    {
        // Older clients posted RemoveCaptain with a CaptainPlacementDto shape that included
        // "orderNumber": 0. ASP.NET Core binds bodies with JsonSerializerDefaults.Web, which
        // skips unknown members, so the field never reaches TeamService.RemoveCaptain.
        var dto = System.Text.Json.JsonSerializer.Deserialize<RemoveCaptainDto>(
            """{"seasonID":7,"personID":201,"orderNumber":0}""",
            WebJson);

        Assert.That(dto, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto!.SeasonID, Is.EqualTo(7));
            Assert.That(dto.PersonID, Is.EqualTo(201));
            Assert.That(typeof(RemoveCaptainDto).GetProperty("OrderNumber"), Is.Null);
        }
    }
}
