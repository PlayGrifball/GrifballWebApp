using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Dtos;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BracketsControllerTests
{
    private ILogger<BracketsController> _logger;
    private IBracketService _bracketService;
    private BracketsController _controller;

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<BracketsController>>();
        _bracketService = Substitute.For<IBracketService>();
        _controller = new BracketsController(_logger, _bracketService);
    }

    [Test]
    public async Task SetCustomSeeds_Should_CallBracketService_When_ValidInputProvided()
    {
        // Arrange
        const int seasonID = 1;
        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 },
            new() { TeamID = 2, Seed = 2 }
        };
        var cancellationToken = CancellationToken.None;

        // Act
        await _controller.SetCustomSeeds(seasonID, customSeeds, cancellationToken);

        // Assert
        await _bracketService.Received(1).SetSeeds(seasonID, customSeeds, cancellationToken);
    }

    [Test]
    public async Task SetCustomSeeds_Should_PassCorrectParameters_When_Called()
    {
        // Arrange
        const int seasonID = 42;
        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 10, Seed = 1 },
            new() { TeamID = 20, Seed = 2 },
            new() { TeamID = 30, Seed = 3 }
        };
        var cancellationToken = new CancellationToken();

        // Act
        await _controller.SetCustomSeeds(seasonID, customSeeds, cancellationToken);

        // Assert
        await _bracketService.Received(1).SetSeeds(
            Arg.Is<int>(x => x == seasonID),
            Arg.Is<CustomSeedDto[]>(x => 
                x.Length == 3 &&
                x[0].TeamID == 10 && x[0].Seed == 1 &&
                x[1].TeamID == 20 && x[1].Seed == 2 &&
                x[2].TeamID == 30 && x[2].Seed == 3),
            Arg.Is<CancellationToken>(x => x == cancellationToken));
    }

    [Test]
    public async Task SetCustomSeeds_Should_PropagateException_When_ServiceThrows()
    {
        // Arrange
        const int seasonID = 1;
        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 }
        };
        var expectedException = new Exception("Test exception");
        _bracketService.SetSeeds(Arg.Any<int>(), Arg.Any<CustomSeedDto[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        // Act & Assert
        var actualException = Assert.ThrowsAsync<Exception>(
            () => _controller.SetCustomSeeds(seasonID, customSeeds, CancellationToken.None));
        
        Assert.That(actualException, Is.EqualTo(expectedException));
    }

    [Test]
    public async Task SetCustomSeeds_Should_HandleEmptyArray_When_NoCustomSeedsProvided()
    {
        // Arrange
        const int seasonID = 1;
        var customSeeds = Array.Empty<CustomSeedDto>();
        var cancellationToken = CancellationToken.None;

        // Act
        await _controller.SetCustomSeeds(seasonID, customSeeds, cancellationToken);

        // Assert
        await _bracketService.Received(1).SetSeeds(seasonID, customSeeds, cancellationToken);
    }
}