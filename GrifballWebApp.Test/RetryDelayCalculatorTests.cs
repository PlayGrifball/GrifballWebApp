using GrifballWebApp.Server.Services;
using NSubstitute;
using System.Net;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RetryDelayCalculatorTests
{
    private const int MaxWaitSeconds = 30;

    /// <summary>
    /// A predictable random that always returns a fixed value for testing.
    /// </summary>
    private class PredictableRandom : Random
    {
        private readonly int _fixedValue;

        public PredictableRandom(int fixedValue)
        {
            _fixedValue = fixedValue;
        }

        public override int Next(int minValue, int maxValue)
        {
            return Math.Min(_fixedValue, maxValue - 1);
        }
    }

    [Test]
    public void CalculateExponentialDelay_ShouldReturnCorrectValues()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));

        // Act & Assert - exponential backoff: 2^(n-1) seconds
        Assert.Multiple(() =>
        {
            Assert.That(calculator.CalculateExponentialDelay(1).TotalSeconds, Is.EqualTo(1));   // 2^0 = 1
            Assert.That(calculator.CalculateExponentialDelay(2).TotalSeconds, Is.EqualTo(2));   // 2^1 = 2
            Assert.That(calculator.CalculateExponentialDelay(3).TotalSeconds, Is.EqualTo(4));   // 2^2 = 4
            Assert.That(calculator.CalculateExponentialDelay(4).TotalSeconds, Is.EqualTo(8));   // 2^3 = 8
            Assert.That(calculator.CalculateExponentialDelay(5).TotalSeconds, Is.EqualTo(16));  // 2^4 = 16
        });
    }

    [Test]
    public void CalculateDelay_WithNoRetryAfterHeader_ShouldUseExponentialBackoffWithJitterAndCap()
    {
        // Arrange - use zero jitter for predictable testing
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act & Assert
        Assert.Multiple(() =>
        {
            // With zero jitter, delay should equal exponential
            Assert.That(calculator.CalculateDelay(1, response).TotalSeconds, Is.EqualTo(1));
            Assert.That(calculator.CalculateDelay(2, response).TotalSeconds, Is.EqualTo(2));
            Assert.That(calculator.CalculateDelay(3, response).TotalSeconds, Is.EqualTo(4));
        });
    }

    [Test]
    public void CalculateDelay_WithNoRetryAfterHeader_ShouldApplyJitter()
    {
        // Arrange - use 500ms jitter
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(500));
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should be exponential (1s) + jitter (500ms) = 1.5s
        Assert.That(delay.TotalMilliseconds, Is.EqualTo(1500));
    }

    [Test]
    public void CalculateDelay_WithNoRetryAfterHeader_ShouldCapAtMaxWaitSeconds()
    {
        // Arrange - high retry attempt that would exceed cap
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        // Act - retry attempt 6 would be 2^5 = 32 seconds, which exceeds 30 second cap
        var delay = calculator.CalculateDelay(6, response);

        // Assert - should be capped at 30 seconds
        Assert.That(delay.TotalSeconds, Is.EqualTo(MaxWaitSeconds));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterSeconds_ShouldUseMaxOfExponentialAndServerValue()
    {
        // Arrange - server says wait 10 seconds, but exponential (retry 1) is only 1 second
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", "10");

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should use server value (10s) since it's larger than exponential (1s)
        Assert.That(delay.TotalSeconds, Is.EqualTo(10));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterSeconds_ShouldUseExponentialWhenLarger()
    {
        // Arrange - server says wait 1 second, but exponential (retry 5) is 16 seconds
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", "1");

        // Act
        var delay = calculator.CalculateDelay(5, response);

        // Assert - should use exponential (16s) since it's larger than server (1s)
        Assert.That(delay.TotalSeconds, Is.EqualTo(16));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterSeconds_ShouldApplyJitter()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(500));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", "5");

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - max(1s exponential, 5s server) + 500ms jitter = 5.5s
        Assert.That(delay.TotalMilliseconds, Is.EqualTo(5500));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterSeconds_ShouldCapAtMaxWaitSeconds()
    {
        // Arrange - server says wait 3600 seconds (1 hour)
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", "3600");

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should be capped at 30 seconds
        Assert.That(delay.TotalSeconds, Is.EqualTo(MaxWaitSeconds));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterDate_ShouldCalculateDelayUntilDate()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var futureDate = DateTimeOffset.UtcNow.AddSeconds(10);
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", futureDate.ToString("R")); // RFC1123 format

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should be approximately 10 seconds (within 1 second tolerance due to timing)
        Assert.That(delay.TotalSeconds, Is.InRange(9, 11));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterDate_ShouldCapAtMaxWaitSeconds()
    {
        // Arrange - date 2 hours in the future
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var farFutureDate = DateTimeOffset.UtcNow.AddHours(2);
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", farFutureDate.ToString("R"));

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should be capped at 30 seconds
        Assert.That(delay.TotalSeconds, Is.EqualTo(MaxWaitSeconds));
    }

    [Test]
    public void CalculateDelay_WithRetryAfterDateInPast_ShouldReturnMinimalDelay()
    {
        // Arrange - date in the past
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(500));
        var pastDate = DateTimeOffset.UtcNow.AddSeconds(-10);
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", pastDate.ToString("R"));

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should return just jitter (500ms)
        Assert.That(delay.TotalMilliseconds, Is.EqualTo(500));
    }

    [Test]
    public void CalculateDelay_WithNullResponse_ShouldUseExponentialBackoff()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));

        // Act
        var delay = calculator.CalculateDelay(3, null);

        // Assert - should use exponential (4s) with jitter
        Assert.That(delay.TotalSeconds, Is.EqualTo(4));
    }

    [Test]
    public void CalculateDelay_WithEmptyRetryAfterHeader_ShouldUseExponentialBackoff()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        // No Retry-After header added

        // Act
        var delay = calculator.CalculateDelay(2, response);

        // Assert - should use exponential (2s) with jitter
        Assert.That(delay.TotalSeconds, Is.EqualTo(2));
    }

    [Test]
    public void CalculateDelay_WithInvalidRetryAfterValue_ShouldUseExponentialBackoff()
    {
        // Arrange - use TryAddWithoutValidation to bypass header format validation
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(0));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.TryAddWithoutValidation("Retry-After", "invalid-value");

        // Act
        var delay = calculator.CalculateDelay(2, response);

        // Assert - should fall through to exponential (2s) since parsing fails
        Assert.That(delay.TotalSeconds, Is.EqualTo(2));
    }

    [Test]
    public void Constructor_WithCustomMaxWaitSeconds_ShouldUseCustomValue()
    {
        // Arrange
        const int customMaxWait = 10;
        var calculator = new RetryDelayCalculator(customMaxWait, new PredictableRandom(0));
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", "3600");

        // Act
        var delay = calculator.CalculateDelay(1, response);

        // Assert - should be capped at custom 10 seconds
        Assert.That(delay.TotalSeconds, Is.EqualTo(customMaxWait));
    }

    [Test]
    public void CalculateDelayWithRetryAfterSeconds_ShouldCombineCorrectly()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(200));

        // Act - exponential = 4s, server = 5s, jitter = 200ms
        var delay = calculator.CalculateDelayWithRetryAfterSeconds(TimeSpan.FromSeconds(4), 5);

        // Assert - max(4, 5) + 0.2 = 5.2s
        Assert.That(delay.TotalMilliseconds, Is.EqualTo(5200));
    }

    [Test]
    public void ApplyJitterAndCap_ShouldApplyJitterCorrectly()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(300));

        // Act
        var delay = calculator.ApplyJitterAndCap(TimeSpan.FromSeconds(5));

        // Assert - 5s + 300ms = 5.3s
        Assert.That(delay.TotalMilliseconds, Is.EqualTo(5300));
    }

    [Test]
    public void ApplyJitterAndCap_ShouldCapWhenExceedsMaxWait()
    {
        // Arrange
        var calculator = new RetryDelayCalculator(MaxWaitSeconds, new PredictableRandom(500));

        // Act - 29s + 500ms would exceed 30s cap
        var delay = calculator.ApplyJitterAndCap(TimeSpan.FromSeconds(29.8));

        // Assert - should be capped at 30s
        Assert.That(delay.TotalSeconds, Is.EqualTo(MaxWaitSeconds));
    }
}
