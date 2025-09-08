using GrifballWebApp.Server;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class UrlServiceTests
{
    private UrlService _service;
    private const string BaseUrl = "https://localhost:4200";

    [SetUp]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseUrl"] = BaseUrl
            })
            .Build();
        _service = new UrlService(configuration);
    }

    [Test]
    public void Constructor_Should_ThrowException_When_BaseUrlIsMissing()
    {
        // Arrange
        var configWithoutBaseUrl = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => new UrlService(configWithoutBaseUrl));
        Assert.That(exception!.Message, Is.EqualTo("Missing BaseUrl from configuration. Should be the home page for the website. Ex: https://localhost:4200"));
    }

    [Test]
    public void SignupForm_Should_ReturnCorrectUrl_When_ValidSeasonIdProvided()
    {
        // Arrange
        const int seasonId = 123;
        const string expectedUrl = "https://localhost:4200/login?followUp=/season/123/signupForm";

        // Act
        var result = _service.SignupForm(seasonId);

        // Assert
        Assert.That(result, Is.EqualTo(expectedUrl));
    }

    [Test]
    public void ViewSignups_Should_ReturnCorrectUrl_When_ValidSeasonIdProvided()
    {
        // Arrange
        const int seasonId = 456;
        const string expectedUrl = "https://localhost:4200/season/456/signups";

        // Act
        var result = _service.ViewSignups(seasonId);

        // Assert
        Assert.That(result, Is.EqualTo(expectedUrl));
    }

    [Test]
    public void Draft_Should_ReturnCorrectUrl_When_ValidSeasonIdProvided()
    {
        // Arrange
        const int seasonId = 789;
        const string expectedUrl = "https://localhost:4200/season/789/teams";

        // Act
        var result = _service.Draft(seasonId);

        // Assert
        Assert.That(result, Is.EqualTo(expectedUrl));
    }

    [Test]
    public void Season_Should_ReturnCorrectUrl_When_ValidSeasonIdProvided()
    {
        // Arrange
        const int seasonId = 101;
        const string expectedUrl = "https://localhost:4200/season/101";

        // Act
        var result = _service.Season(seasonId);

        // Assert
        Assert.That(result, Is.EqualTo(expectedUrl));
    }

    [Test]
    public void AllMethods_Should_HandleNegativeSeasonId()
    {
        // Arrange
        const int negativeSeasonId = -1;

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(_service.SignupForm(negativeSeasonId), Is.EqualTo("https://localhost:4200/login?followUp=/season/-1/signupForm"));
            Assert.That(_service.ViewSignups(negativeSeasonId), Is.EqualTo("https://localhost:4200/season/-1/signups"));
            Assert.That(_service.Draft(negativeSeasonId), Is.EqualTo("https://localhost:4200/season/-1/teams"));
            Assert.That(_service.Season(negativeSeasonId), Is.EqualTo("https://localhost:4200/season/-1"));
        });
    }

    [Test]
    public void AllMethods_Should_HandleZeroSeasonId()
    {
        // Arrange
        const int zeroSeasonId = 0;

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(_service.SignupForm(zeroSeasonId), Is.EqualTo("https://localhost:4200/login?followUp=/season/0/signupForm"));
            Assert.That(_service.ViewSignups(zeroSeasonId), Is.EqualTo("https://localhost:4200/season/0/signups"));
            Assert.That(_service.Draft(zeroSeasonId), Is.EqualTo("https://localhost:4200/season/0/teams"));
            Assert.That(_service.Season(zeroSeasonId), Is.EqualTo("https://localhost:4200/season/0"));
        });
    }

    [Test]
    public void AllMethods_Should_UseConfiguredBaseUrl_When_DifferentBaseUrlProvided()
    {
        // Arrange
        const string customBaseUrl = "https://prod.example.com";
        const int seasonId = 42;
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseUrl"] = customBaseUrl
            })
            .Build();
        var customService = new UrlService(customConfig);

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.That(customService.SignupForm(seasonId), Is.EqualTo("https://prod.example.com/login?followUp=/season/42/signupForm"));
            Assert.That(customService.ViewSignups(seasonId), Is.EqualTo("https://prod.example.com/season/42/signups"));
            Assert.That(customService.Draft(seasonId), Is.EqualTo("https://prod.example.com/season/42/teams"));
            Assert.That(customService.Season(seasonId), Is.EqualTo("https://prod.example.com/season/42"));
        });
    }
}