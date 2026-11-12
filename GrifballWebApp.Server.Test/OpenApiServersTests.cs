namespace GrifballWebApp.Server.Test;

/// <summary>
/// The OpenAPI document's "servers" list (Program.cs, AddSwaggerGen's PreSerializeFilters): the request's own URL
/// first, then every configured environment that isn't it, then in Development only the backend itself.
/// </summary>
[TestFixture]
public class OpenApiServersTests
{
    private static readonly string[] DevelopmentOrder =
    [
        "http://localhost/api",
        "https://grifball.xyz/api",
        "https://grif-staging.grifball.xyz/api",
        "/",
    ];

    private static OpenApiServerEntry Entry(string url, string description) => new() { Url = url, Description = description };

    [Test]
    public void Build_PutsTheRequestsOwnUrlFirst()
    {
        var servers = OpenApiServers.Build("https://grifball.xyz/api",
            [Entry("https://grif-staging.grifball.xyz/api", "Staging")], isDevelopment: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(servers[0].Url, Is.EqualTo("https://grifball.xyz/api"));
            Assert.That(servers[0].Description, Is.EqualTo("This environment"));
            Assert.That(servers, Has.Count.EqualTo(2));
            Assert.That(servers[1].Url, Is.EqualTo("https://grif-staging.grifball.xyz/api"));
        }
    }

    [TestCase("https://grifball.xyz/api")]
    [TestCase("https://grifball.xyz/api/")]
    [TestCase("HTTPS://GRIFBALL.XYZ/API")]
    public void Build_SkipsAConfiguredEntryThatIsTheRequestsOwnUrl(string requestUrl)
    {
        var servers = OpenApiServers.Build(requestUrl,
            [Entry("https://grifball.xyz/api", "Production"), Entry("https://grif-staging.grifball.xyz/api", "Staging")], isDevelopment: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(servers, Has.Count.EqualTo(2), "The duplicate of the request's own URL is left out");
            Assert.That(servers[0].Url, Is.EqualTo(requestUrl));
            Assert.That(servers[1].Url, Is.EqualTo("https://grif-staging.grifball.xyz/api"));
        }
    }

    [Test]
    public void Build_AppendsTheUnproxiedBackend_InDevelopmentOnly()
    {
        var inDevelopment = OpenApiServers.Build("http://localhost/api", [], isDevelopment: true);
        var outsideDevelopment = OpenApiServers.Build("http://localhost/api", [], isDevelopment: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inDevelopment.Select(s => s.Url), Does.Contain("/"));
            Assert.That(outsideDevelopment.Select(s => s.Url), Does.Not.Contain("/"));
        }
    }

    [Test]
    public void Build_OrdersTheConfiguredEntriesAfterTheRequestsOwnUrl_ThenDevelopmentLast()
    {
        var servers = OpenApiServers.Build("http://localhost/api",
            [Entry("https://grifball.xyz/api", "Production"), Entry("https://grif-staging.grifball.xyz/api", "Staging")], isDevelopment: true);

        Assert.That(servers.Select(s => s.Url), Is.EqualTo(DevelopmentOrder));
    }
}
