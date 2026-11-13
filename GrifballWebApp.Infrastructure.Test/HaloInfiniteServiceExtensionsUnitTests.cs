using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Core.Storage;
using Surprenant.Grunt.Gateway.Client;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// AddHaloInfinite: what each HaloInfinite:Mode registers for IHaloInfiniteClient, and the health check's ViaGateway.
/// A WebApplicationBuilder's own container, never run: no database, host, file or real network.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloInfiniteServiceExtensionsUnitTests
{
    // CreateEmptyBuilder, not CreateBuilder: no appsettings*.json, no environment variables, no ambient defaults
    // that could change the result.
    private static WebApplicationBuilder Builder(params (string Key, string? Value)[] values)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.Services.AddLogging();
        builder.Configuration.AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)));
        return builder;
    }

    private static ServiceProvider Register(WebApplicationBuilder builder)
    {
        builder.AddHaloInfinite(HaloInfiniteSettings.FromConfiguration(builder.Configuration), new RetryDelayCalculator());
        return builder.Services.BuildServiceProvider();
    }

    [Test]
    public void Gateway_RegistersTheGatewayClient_AndNoSignIn()
    {
        using var services = Register(Builder(
            (HaloInfiniteSettings.ModeKey, "Gateway"),
            (HaloInfiniteSettings.BaseUrlKey, "http://gw.test:8080"),
            (HaloInfiniteSettings.ApiKeyKey, "the-key"),
            ("HaloGateway:Retry:MaxAttempts", "5")));

        var options = services.GetRequiredService<IOptions<HaloGatewayClientOptions>>().Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.GetRequiredService<IHaloInfiniteClient>(), Is.TypeOf<HaloGatewayClient>());
            Assert.That(options.BaseAddress, Is.EqualTo(new Uri("http://gw.test:8080/")));
            Assert.That(options.ApiKey, Is.EqualTo("the-key"));
            Assert.That(options.Retry.MaxAttempts, Is.EqualTo(5));
            Assert.That(services.GetService<IHaloInfiniteClientFactory>(), Is.Null);
            Assert.That(services.GetService<IAccountAuthorization>(), Is.Null);
            Assert.That(services.GetService<IHaloGatewayAccountStatus>(), Is.TypeOf<HaloGatewayAccountStatus>());
            Assert.That(services.GetRequiredService<HaloInfiniteHealthCheck>().ViaGateway, Is.True);
            Assert.That(services.GetRequiredService<HaloInfiniteSettings>().Mode, Is.EqualTo(HaloInfiniteMode.Gateway));
        }
    }

    [Test]
    public void Direct_RegistersTheFactoryAsTheClient()
    {
        using var services = Register(Builder((HaloInfiniteSettings.ModeKey, "Direct")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.GetRequiredService<IHaloInfiniteClientFactory>(), Is.TypeOf<HaloInfiniteClientFactory>());
            Assert.That(services.GetRequiredService<IHaloInfiniteClient>(), Is.SameAs(services.GetRequiredService<IHaloInfiniteClientFactory>()));
            Assert.That(services.GetServices<IHaloInfiniteClient>().Count(), Is.EqualTo(1));
            Assert.That(services.GetService<IAccountAuthorization>(), Is.Not.Null);
            Assert.That(services.GetService<IHaloGatewayAccountStatus>(), Is.Null);
            Assert.That(services.GetService<HaloGatewayClient>(), Is.Null);
            Assert.That(services.GetRequiredService<HaloInfiniteHealthCheck>().ViaGateway, Is.False);
            Assert.That(services.GetRequiredService<HaloInfiniteSettings>().Mode, Is.EqualTo(HaloInfiniteMode.Direct));
        }
    }
}
