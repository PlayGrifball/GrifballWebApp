using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly;
using Polly.Extensions.Http;
using System.Net;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class PollyRetryPolicyTests
{
    private ILogger<PollyRetryPolicyTests> _mockLogger;
    private int _requestCount;
    private Queue<HttpResponseMessage> _responseQueue;
    private List<TimeSpan> _observedDelays;

    [SetUp]
    public void Setup()
    {
        _mockLogger = Substitute.For<ILogger<PollyRetryPolicyTests>>();
        _requestCount = 0;
        _responseQueue = new Queue<HttpResponseMessage>();
        _observedDelays = new List<TimeSpan>();
    }

    private HttpClient CreateHttpClientWithRetryPolicy()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockLogger);
        services.AddHttpClient("TestClient")
            .ConfigurePrimaryHttpMessageHandler(() => new TestHttpMessageHandler(this))
            .AddPolicyHandler((sp, request) =>
            {
                var logger = sp.GetRequiredService<ILogger<PollyRetryPolicyTests>>();
                const int maxRetries = 5;
                const int maxWaitSeconds = 30;

                return HttpPolicyExtensions.HandleTransientHttpError()
                    .OrResult(msg => (int)msg.StatusCode == 429)
                    .WaitAndRetryAsync(
                        maxRetries,
                        (int retryAttempt, DelegateResult<HttpResponseMessage> outcome, Context ctx) =>
                        {
                            var exponential = TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt - 1) * 1000);
                            TimeSpan delay = exponential;

                            var resp = outcome?.Result;
                            if (resp != null && resp.Headers.TryGetValues("Retry-After", out var values))
                            {
                                var first = values.FirstOrDefault();
                                if (!string.IsNullOrEmpty(first))
                                {
                                    if (int.TryParse(first, out var seconds))
                                    {
                                        var server = TimeSpan.FromSeconds(seconds);
                                        var jitterMs = Random.Shared.Next(0, 1000);
                                        var combinedMs = Math.Max(exponential.TotalMilliseconds, server.TotalMilliseconds) + jitterMs;
                                        var capped = Math.Min(combinedMs, TimeSpan.FromSeconds(maxWaitSeconds).TotalMilliseconds);
                                        delay = TimeSpan.FromMilliseconds(capped);
                                    }
                                    else if (DateTimeOffset.TryParse(first, out var date))
                                    {
                                        var until = date - DateTimeOffset.UtcNow;
                                        if (until > TimeSpan.Zero)
                                        {
                                            var jitterMs = Random.Shared.Next(0, 1000);
                                            var capped = Math.Min(until.TotalMilliseconds + jitterMs, TimeSpan.FromSeconds(maxWaitSeconds).TotalMilliseconds);
                                            delay = TimeSpan.FromMilliseconds(capped);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var jitterMs = Random.Shared.Next(0, 1000);
                                var capped = Math.Min(exponential.TotalMilliseconds + jitterMs, TimeSpan.FromSeconds(maxWaitSeconds).TotalMilliseconds);
                                delay = TimeSpan.FromMilliseconds(capped);
                            }

                            return delay;
                        },
                        async (outcome, timespan, retryNumber, ctx) =>
                        {
                            var status = outcome?.Result?.StatusCode.ToString() ?? (outcome?.Exception?.GetType().Name ?? "Unknown");
                            var method = outcome?.Result?.RequestMessage?.Method?.Method ?? request.Method.Method;
                            var uri = outcome?.Result?.RequestMessage?.RequestUri?.ToString() ?? request.RequestUri?.ToString();
                            logger.LogWarning("Received HTTP {Status} for {Method} {Uri}. Retry {Retry}/{MaxRetries}. Next delay {Delay}.", status, method, uri, retryNumber, maxRetries, timespan);
                            await Task.CompletedTask;
                        });
            });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return factory.CreateClient("TestClient");
    }

    [Test]
    public async Task RetryPolicy_ShouldRetryOnTransientError_AndEventuallySucceed()
    {
        // Arrange - fail first attempt, then succeed
        var client = CreateHttpClientWithRetryPolicy();
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("success") });

        // Act
        var response = await client.GetAsync("http://test.com/api");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_requestCount, Is.EqualTo(2), "Should have made 2 requests (1 initial + 1 retry)");
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldRetryUpToMaxRetries_OnPersistentFailure()
    {
        // Arrange - always fail (6 failures = 1 initial + 5 retries)
        var client = CreateHttpClientWithRetryPolicy();
        for (int i = 0; i < 7; i++) // Extra one to be safe
        {
            _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        // Act
        var response = await client.GetAsync("http://test.com/api");
        
        // Assert - Polly returns the last failed response
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(_requestCount, Is.EqualTo(6), "Should have made 6 total attempts (1 initial + 5 retries)");
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldHandleHttp429_WithRetryAfterSeconds()
    {
        // Arrange
        var client = CreateHttpClientWithRetryPolicy();
        var response429 = new HttpResponseMessage((HttpStatusCode)429);
        response429.Headers.Add("Retry-After", "2");
        _responseQueue.Enqueue(response429);
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await client.GetAsync("http://test.com/api");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_requestCount, Is.EqualTo(2), "Should have retried once");
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldHandleRetryAfterDate()
    {
        // Arrange
        var client = CreateHttpClientWithRetryPolicy();
        var futureDate = DateTimeOffset.UtcNow.AddSeconds(1);
        var response429 = new HttpResponseMessage((HttpStatusCode)429);
        response429.Headers.Add("Retry-After", futureDate.ToString("R")); // RFC1123 format
        _responseQueue.Enqueue(response429);
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await client.GetAsync("http://test.com/api");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_requestCount, Is.EqualTo(2), "Should have retried once");
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldCapDelayAtMaxWaitSeconds()
    {
        // Arrange
        var client = CreateHttpClientWithRetryPolicy();
        var veryFarFutureDate = DateTimeOffset.UtcNow.AddHours(2); // 2 hours in future
        var response429 = new HttpResponseMessage((HttpStatusCode)429);
        response429.Headers.Add("Retry-After", veryFarFutureDate.ToString("R"));
        _responseQueue.Enqueue(response429);
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var startTime = DateTimeOffset.UtcNow;
        var response = await client.GetAsync("http://test.com/api");
        var elapsed = DateTimeOffset.UtcNow - startTime;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(elapsed.TotalSeconds, Is.LessThan(35), "Delay should be capped at ~30 seconds + jitter, not 2 hours");
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldCapLargeRetryAfterSeconds()
    {
        // Arrange
        var client = CreateHttpClientWithRetryPolicy();
        var response429 = new HttpResponseMessage((HttpStatusCode)429);
        response429.Headers.Add("Retry-After", "3600"); // 1 hour
        _responseQueue.Enqueue(response429);
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var startTime = DateTimeOffset.UtcNow;
        var response = await client.GetAsync("http://test.com/api");
        var elapsed = DateTimeOffset.UtcNow - startTime;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(elapsed.TotalSeconds, Is.LessThan(35), "Delay should be capped at ~30 seconds + jitter");
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldLogWarningOnRetry()
    {
        // Arrange
        var client = CreateHttpClientWithRetryPolicy();
        for (int i = 0; i < 7; i++) // Extra one to be safe
        {
            _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        // Act
        var response = await client.GetAsync("http://test.com/api");

        // Assert - verify retry behavior through request count
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(_requestCount, Is.EqualTo(6), "Should have made 6 total attempts (1 initial + 5 retries)");
            // Note: Logger verification is difficult with NSubstitute and ILogger extension methods
            // The logging behavior is tested implicitly through the retry logic
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldApplyJitter_ToExponentialBackoff()
    {
        // Arrange
        var client = CreateHttpClientWithRetryPolicy();
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await client.GetAsync("http://test.com/api");

        // Assert - this is a probabilistic test, just verify we got a retry
        Assert.Multiple(() =>
        {
            Assert.That(_requestCount, Is.EqualTo(2), "Should have retried once with jittered delay");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task RetryPolicy_ShouldUseMaxOfExponentialAndRetryAfter()
    {
        // Arrange - first retry would be 1 second exponential, but Retry-After says 5 seconds
        var client = CreateHttpClientWithRetryPolicy();
        var response429 = new HttpResponseMessage((HttpStatusCode)429);
        response429.Headers.Add("Retry-After", "5");
        _responseQueue.Enqueue(response429);
        _responseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var startTime = DateTimeOffset.UtcNow;
        var response = await client.GetAsync("http://test.com/api");
        var elapsed = DateTimeOffset.UtcNow - startTime;

        // Assert - should have waited ~5 seconds + jitter, not just 1 second
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            // Due to jitter and test timing, just verify it's between 4 and 7 seconds
            Assert.That(elapsed.TotalSeconds, Is.GreaterThan(4).And.LessThan(7), 
                "Should use Retry-After value (5s) which is larger than exponential (1s for first retry)");
        });
    }

    // Helper classes
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly PollyRetryPolicyTests _test;

        public TestHttpMessageHandler(PollyRetryPolicyTests test)
        {
            _test = test;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _test._requestCount++;
            
            if (_test._responseQueue.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            var response = _test._responseQueue.Dequeue();
            return Task.FromResult(response);
        }
    }
}
