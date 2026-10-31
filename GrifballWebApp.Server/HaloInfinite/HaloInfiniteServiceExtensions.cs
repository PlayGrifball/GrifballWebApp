using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.Extensions.Http;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Gateway.Client;
using Surprenant.Grunt.Util;
using System.Net;

namespace GrifballWebApp.Server.HaloInfinite;

public static class HaloInfiniteServiceExtensions
{
    /// <summary>The HttpClient <see cref="HaloGatewayAccountStatus"/> reads the gateway's <c>/health</c> with.</summary>
    public const string GatewayStatusClientName = "HaloGatewayStatus";

    /// <summary>How long one gateway <c>/health</c> request may take.</summary>
    public static readonly TimeSpan GatewayStatusTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Registers <see cref="IHaloInfiniteClient"/>, which every Halo call in the app goes through, for the mode in
    /// <paramref name="settings"/>, plus the settings themselves and <see cref="HaloInfiniteHealthCheck"/> (the
    /// <c>halo_infinite</c> check and the Infinite Client page's status):
    /// <list type="bullet">
    /// <item><b>Gateway</b>: Grunt's <see cref="HaloGatewayClient"/> at <c>HaloGateway:BaseUrl</c> with
    /// <c>HaloGateway:ApiKey</c> (transport retries from <c>HaloGateway:Retry</c>), and
    /// <see cref="IHaloGatewayAccountStatus"/>. Nothing of the app's own sign-in is registered.</item>
    /// <item><b>Direct</b>: as before the gateway, Grunt's <see cref="IHaloInfiniteClientFactory"/> with its file token
    /// storage and <c>ClientConfiguration</c>, and the retry policy on the HttpClient its clients use.
    /// <see cref="IHaloInfiniteClient"/> is the factory itself, which signs in and proxies each call to a client with a
    /// valid Spartan token.</item>
    /// </list>
    /// </summary>
    public static HaloInfiniteSettings AddHaloInfinite(this WebApplicationBuilder builder, HaloInfiniteSettings settings, IRetryDelayCalculator retryDelayCalculator)
    {
        var services = builder.Services;
        services.AddSingleton(settings);

        if (settings.IsGateway)
        {
            services.AddHaloGatewayClient(options =>
            {
                options.BaseAddress = settings.GatewayBaseUrl;
                options.ApiKey = settings.GatewayApiKey;
                builder.Configuration.GetSection(HaloInfiniteSettings.GatewaySection + ":Retry").Bind(options.Retry);
            });

            services.AddHttpClient(GatewayStatusClientName, client =>
            {
                client.BaseAddress = settings.GatewayBaseUrl;
                client.Timeout = GatewayStatusTimeout;
            });
            services.TryAddSingleton<IHaloGatewayAccountStatus, HaloGatewayAccountStatus>();
        }
        else
        {
            builder.RegisterHaloInfiniteClientFactory();

            // The HttpClient the factory's clients use (named after IHaloInfiniteClient): Halo's 429s and transient errors.
            services.AddHttpClient<IHaloInfiniteClient, HaloInfiniteClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                })
                .AddPolicyHandler((sp, request) =>
                {
                    var logger = sp.GetRequiredService<ILogger<HaloInfiniteSettings>>();
                    const int maxRetries = 5;

                    return HttpPolicyExtensions.HandleTransientHttpError()
                        .OrResult(msg => (int)msg.StatusCode == 429)
                        .WaitAndRetryAsync(
                            maxRetries,
                            (int retryAttempt, DelegateResult<HttpResponseMessage> outcome, Context ctx) =>
                            {
                                return retryDelayCalculator.CalculateDelay(retryAttempt, outcome?.Result);
                            },
                            (outcome, timespan, retryNumber, ctx) =>
                            {
                                var status = outcome?.Result?.StatusCode.ToString() ?? (outcome?.Exception?.GetType().Name ?? "Unknown");
                                logger.LogWarning("Received HTTP {Status} for {Method} {Uri}. Retry {Retry}/{MaxRetries}. Next delay {Delay}.", status, request.Method, request.RequestUri, retryNumber, maxRetries, timespan);
                                return Task.CompletedTask;
                            });
                });

            // Both registrations above made IHaloInfiniteClient a bare client with no token; the app's calls go through the
            // factory instead. The named HttpClient configuration stays.
            services.RemoveAll<IHaloInfiniteClient>();
            services.AddSingleton<IHaloInfiniteClient>(sp => sp.GetRequiredService<IHaloInfiniteClientFactory>());
        }

        // A singleton, so the cached probe result outlives each run (AddAppHealthChecks registers the same if it runs first).
        services.TryAddSingleton(sp => HaloInfiniteHealthCheck.FromConfiguration(sp, builder.Configuration));
        return settings;
    }
}
