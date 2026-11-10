using static GrifballWebApp.Server.Test.MetricsAndTracingExtensionsTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>The meter and tracer providers with an OTLP endpoint: disposing them exports to it (a socket to the endpoint,
/// here one nothing listens on).</summary>
[TestFixture]
public class MetricsAndTracingExportTests
{
    [TestCase("http://127.0.0.1:9", null, false)]
    [TestCase("http://127.0.0.1:9", "http/protobuf", true)]
    public void Providers_BuildWithAnOtlpEndpoint(string endpoint, string? protocol, bool console) => AssertProvidersBuild(endpoint, protocol, console);
}
