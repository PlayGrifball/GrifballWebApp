using GrifballWebApp.Server.Dtos;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CustomSeedDtoTests
{
    [Test]
    public void CustomSeedDto_Should_HaveCorrectProperties_When_Instantiated()
    {
        // Arrange & Act
        var customSeedDto = new CustomSeedDto
        {
            TeamID = 123,
            Seed = 1
        };

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(customSeedDto.TeamID, Is.EqualTo(123), "TeamID should be set correctly");
            Assert.That(customSeedDto.Seed, Is.EqualTo(1), "Seed should be set correctly");
        });
    }

    [Test]
    public void CustomSeedDto_Should_SupportRecordEquality_When_ComparingInstances()
    {
        // Arrange
        var dto1 = new CustomSeedDto { TeamID = 123, Seed = 1 };
        var dto2 = new CustomSeedDto { TeamID = 123, Seed = 1 };
        var dto3 = new CustomSeedDto { TeamID = 456, Seed = 2 };

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(dto1, Is.EqualTo(dto2), "DTOs with same values should be equal");
            Assert.That(dto1, Is.Not.EqualTo(dto3), "DTOs with different values should not be equal");
            Assert.That(dto1.GetHashCode(), Is.EqualTo(dto2.GetHashCode()), "DTOs with same values should have same hash code");
        });
    }

    [Test]
    public void CustomSeedDto_Should_HandleDifferentTeamIDs_When_SeedIsSame()
    {
        // Arrange
        var dto1 = new CustomSeedDto { TeamID = 1, Seed = 1 };
        var dto2 = new CustomSeedDto { TeamID = 2, Seed = 1 };

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(dto1.Seed, Is.EqualTo(dto2.Seed), "Both DTOs should have same seed");
            Assert.That(dto1.TeamID, Is.Not.EqualTo(dto2.TeamID), "DTOs should have different team IDs");
            Assert.That(dto1, Is.Not.EqualTo(dto2), "DTOs with different team IDs should not be equal");
        });
    }

    [Test]
    public void CustomSeedDto_Should_HandleDifferentSeeds_When_TeamIDIsSame()
    {
        // Arrange
        var dto1 = new CustomSeedDto { TeamID = 1, Seed = 1 };
        var dto2 = new CustomSeedDto { TeamID = 1, Seed = 2 };

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(dto1.TeamID, Is.EqualTo(dto2.TeamID), "Both DTOs should have same team ID");
            Assert.That(dto1.Seed, Is.Not.EqualTo(dto2.Seed), "DTOs should have different seeds");
            Assert.That(dto1, Is.Not.EqualTo(dto2), "DTOs with different seeds should not be equal");
        });
    }
}