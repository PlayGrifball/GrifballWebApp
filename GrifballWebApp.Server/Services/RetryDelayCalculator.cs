using System.Net;

namespace GrifballWebApp.Server.Services;

/// <summary>
/// Calculates retry delays for HTTP requests with exponential backoff, 
/// Retry-After header support, jitter, and capping.
/// </summary>
public interface IRetryDelayCalculator
{
    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="retryAttempt">The current retry attempt number (1-based).</param>
    /// <param name="response">The HTTP response that triggered the retry, or null if retry is due to an exception.</param>
    /// <returns>The delay to wait before the next retry attempt.</returns>
    TimeSpan CalculateDelay(int retryAttempt, HttpResponseMessage? response);
}

/// <summary>
/// Default implementation of retry delay calculation with exponential backoff,
/// Retry-After header support, jitter, and maximum delay capping.
/// </summary>
public class RetryDelayCalculator : IRetryDelayCalculator
{
    private readonly int _maxWaitSeconds;
    private readonly Random _random;

    /// <summary>
    /// Creates a new instance of RetryDelayCalculator.
    /// </summary>
    /// <param name="maxWaitSeconds">Maximum delay in seconds (default: 30).</param>
    /// <param name="random">Random instance for jitter calculation. If null, uses Random.Shared.</param>
    public RetryDelayCalculator(int maxWaitSeconds = 30, Random? random = null)
    {
        _maxWaitSeconds = maxWaitSeconds;
        _random = random ?? Random.Shared;
    }

    /// <inheritdoc/>
    public TimeSpan CalculateDelay(int retryAttempt, HttpResponseMessage? response)
    {
        var exponential = CalculateExponentialDelay(retryAttempt);
        TimeSpan delay = exponential;

        if (response != null && response.Headers.TryGetValues("Retry-After", out var values))
        {
            var first = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(first))
            {
                if (int.TryParse(first, out var seconds))
                {
                    delay = CalculateDelayWithRetryAfterSeconds(exponential, seconds);
                }
                else if (DateTimeOffset.TryParse(first, out var date))
                {
                    delay = CalculateDelayWithRetryAfterDate(date);
                }
            }
        }
        else
        {
            delay = ApplyJitterAndCap(exponential);
        }

        return delay;
    }

    /// <summary>
    /// Calculates exponential backoff delay: 2^(retryAttempt-1) seconds.
    /// </summary>
    public TimeSpan CalculateExponentialDelay(int retryAttempt)
    {
        return TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt - 1) * 1000);
    }

    /// <summary>
    /// Calculates delay when Retry-After header contains seconds.
    /// Uses max of exponential and server-specified delay, plus jitter, capped at max wait.
    /// </summary>
    public TimeSpan CalculateDelayWithRetryAfterSeconds(TimeSpan exponential, int seconds)
    {
        var server = TimeSpan.FromSeconds(seconds);
        var jitterMs = _random.Next(0, 1000);
        var combinedMs = Math.Max(exponential.TotalMilliseconds, server.TotalMilliseconds) + jitterMs;
        var capped = Math.Min(combinedMs, TimeSpan.FromSeconds(_maxWaitSeconds).TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }

    /// <summary>
    /// Calculates delay when Retry-After header contains a date.
    /// Uses time until the specified date plus jitter, capped at max wait.
    /// </summary>
    public TimeSpan CalculateDelayWithRetryAfterDate(DateTimeOffset date)
    {
        var until = date - DateTimeOffset.UtcNow;
        if (until > TimeSpan.Zero)
        {
            var jitterMs = _random.Next(0, 1000);
            var capped = Math.Min(until.TotalMilliseconds + jitterMs, TimeSpan.FromSeconds(_maxWaitSeconds).TotalMilliseconds);
            return TimeSpan.FromMilliseconds(capped);
        }
        // If date is in the past, return minimal delay with jitter
        return TimeSpan.FromMilliseconds(_random.Next(0, 1000));
    }

    /// <summary>
    /// Applies jitter and caps the delay at maximum wait time.
    /// </summary>
    public TimeSpan ApplyJitterAndCap(TimeSpan baseDelay)
    {
        var jitterMs = _random.Next(0, 1000);
        var capped = Math.Min(baseDelay.TotalMilliseconds + jitterMs, TimeSpan.FromSeconds(_maxWaitSeconds).TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }
}
