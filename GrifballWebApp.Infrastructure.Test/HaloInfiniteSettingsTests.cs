using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>HaloInfiniteSettings.FromConfiguration: pure parsing of configuration values, no database, host or network.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloInfiniteSettingsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void FromConfiguration_NoSettings_DefaultsToGateway()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config([]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Mode, Is.EqualTo(HaloInfiniteMode.Gateway));
            Assert.That(settings.IsGateway, Is.True);
            Assert.That(settings.GatewayBaseUrl, Is.EqualTo(new Uri(HaloInfiniteSettings.DefaultGatewayBaseUrl)));
            Assert.That(settings.GatewayApiKey, Is.Null);
            Assert.That(settings.GatewayAdminUrl, Is.EqualTo(HaloInfiniteSettings.DefaultGatewayAdminUrl));
        }
    }

    [TestCase("Gateway", HaloInfiniteMode.Gateway)]
    [TestCase("gateway", HaloInfiniteMode.Gateway)]
    [TestCase("Direct", HaloInfiniteMode.Direct)]
    [TestCase("DIRECT", HaloInfiniteMode.Direct)]
    [TestCase(" Direct ", HaloInfiniteMode.Direct)]
    public void FromConfiguration_Mode_IsCaseInsensitiveAndTrimmed(string mode, HaloInfiniteMode expected)
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.ModeKey] = mode }));

        Assert.That(settings.Mode, Is.EqualTo(expected));
    }

    [TestCase("Bogus")]
    [TestCase("7")]
    public void FromConfiguration_UnknownMode_Throws(string mode)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.ModeKey] = mode })));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain(HaloInfiniteSettings.ModeKey));
            Assert.That(ex.Message, Does.Contain(mode));
        }
    }

    [Test]
    public void FromConfiguration_BaseUrlWithoutTrailingSlash_GetsOne()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.BaseUrlKey] = "https://gateway.test/halo" }));

        Assert.That(settings.GatewayBaseUrl, Is.EqualTo(new Uri("https://gateway.test/halo/")));
    }

    [Test]
    public void FromConfiguration_BaseUrlWithTrailingSlash_IsKeptAsIs()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.BaseUrlKey] = "https://gateway.test/halo/" }));

        Assert.That(settings.GatewayBaseUrl, Is.EqualTo(new Uri("https://gateway.test/halo/")));
    }

    [TestCase("not a url")]
    [TestCase("ftp://gateway.test/")]
    [TestCase("relative/path")]
    public void FromConfiguration_InvalidBaseUrl_Throws(string baseUrl)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.BaseUrlKey] = baseUrl })));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain(HaloInfiniteSettings.BaseUrlKey));
            Assert.That(ex.Message, Does.Contain(baseUrl));
        }
    }

    [Test]
    public void FromConfiguration_ApiKeyAndAdminUrl_AreTrimmedAndKept()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new()
        {
            [HaloInfiniteSettings.ApiKeyKey] = " secret-key ",
            [HaloInfiniteSettings.AdminUrlKey] = " https://grunt.test/admin ",
        }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.GatewayApiKey, Is.EqualTo("secret-key"));
            Assert.That(settings.GatewayAdminUrl, Is.EqualTo("https://grunt.test/admin"));
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void FromConfiguration_BlankApiKey_IsNull(string? apiKey)
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.ApiKeyKey] = apiKey }));

        Assert.That(settings.GatewayApiKey, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    public void FromConfiguration_BlankAdminUrl_UsesDefault(string? adminUrl)
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.AdminUrlKey] = adminUrl }));

        Assert.That(settings.GatewayAdminUrl, Is.EqualTo(HaloInfiniteSettings.DefaultGatewayAdminUrl));
    }

    [Test]
    public void ToString_Gateway_NamesTheUrlAndWhetherTheKeyIsSet()
    {
        var withKey = HaloInfiniteSettings.FromConfiguration(Config(new()
        {
            [HaloInfiniteSettings.BaseUrlKey] = "https://gateway.test/",
            [HaloInfiniteSettings.ApiKeyKey] = "secret",
        }));
        var withoutKey = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.BaseUrlKey] = "https://gateway.test/" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withKey.ToString(), Does.Contain("https://gateway.test/").And.Contain("API key set"));
            Assert.That(withoutKey.ToString(), Does.Contain("API key not set"));
            Assert.That(withKey.ToString(), Does.Not.Contain("secret"), "Never logs the key itself");
        }
    }

    [Test]
    public void ToString_Direct_NamesTheAppsOwnSignIn()
    {
        var settings = HaloInfiniteSettings.FromConfiguration(Config(new() { [HaloInfiniteSettings.ModeKey] = "Direct" }));

        Assert.That(settings.ToString(), Does.Contain("direct"));
    }
}
