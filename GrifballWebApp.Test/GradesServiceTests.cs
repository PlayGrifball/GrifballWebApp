using GrifballWebApp.Database;
using GrifballWebApp.Server.Grades;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GradesServiceTests
{
    private GrifballContext _context;
    private GradesService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new GradesService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetGrades_Should_ReturnEmptyGradesDto_When_NoMatchParticipantsExist()
    {
        // Arrange
        const int seasonId = 1;

        // Act
        var result = await _service.GetGrades(seasonId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Totals, Is.Empty);
        Assert.That(result.PerMinutes, Is.Empty);
    }
}
