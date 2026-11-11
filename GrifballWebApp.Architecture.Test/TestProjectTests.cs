namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// The test projects' <c>&lt;ProjectReference&gt;</c>s follow the layers too: a unit test project references its layer,
/// the layers below it and the unit test projects below it (for their helpers); the integration tests reference anything
/// but the architecture tests; the architecture tests reference the production projects alone; and nothing references
/// the integration or the architecture tests. A new test project is classified here before it can pass.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TestProjectTests
{
    private const string Domain = "GrifballWebApp.Domain";
    private const string Application = "GrifballWebApp.Application";
    private const string Infrastructure = "GrifballWebApp.Infrastructure";
    private const string Server = "GrifballWebApp.Server";
    private const string DomainTest = Domain + ".Test";
    private const string ApplicationTest = Application + ".Test";
    private const string InfrastructureTest = Infrastructure + ".Test";
    private const string ServerTest = Server + ".Test";
    private const string SeederApp = "GrifballWebApp.Seeder";
    private const string ConvertApp = "GrifballWebApp.Convert";
    private const string SeederTest = SeederApp + ".Test";
    private const string ConvertTest = ConvertApp + ".Test";
    private const string Analyzers = "GrifballWebApp.Analyzers";
    private const string AnalyzersTest = Analyzers + ".Test";
    private const string IntegrationTest = "GrifballWebApp.Integration.Test";
    private const string ArchitectureTest = "GrifballWebApp.Architecture.Test";

    /// <summary>
    /// Every test project, with the projects it may reference: the unit test projects by name, the integration and the
    /// architecture tests by rule (null: <see cref="IntegrationTests_ReferenceAnyProject_ButTheArchitectureTests"/> and
    /// <see cref="ArchitectureTests_ReferenceProductionProjectsOnly"/>). To add a test project, list it here.
    /// </summary>
    private static readonly Dictionary<string, string[]?> Known = new()
    {
        [DomainTest] = [Domain],
        [ApplicationTest] = [Application, Domain, DomainTest],
        [InfrastructureTest] = [Infrastructure, Application, Domain, DomainTest, ApplicationTest],
        [ServerTest] = [Server, Infrastructure, Application, Domain, DomainTest, ApplicationTest, InfrastructureTest],
        // The host apps beside the server, each with its own unit tests
        [SeederTest] = [SeederApp, Infrastructure, Application, Domain, DomainTest, ApplicationTest, InfrastructureTest],
        [ConvertTest] = [ConvertApp, Infrastructure, Application, Domain, DomainTest, ApplicationTest, InfrastructureTest],
        // The analyzers every project builds with (Directory.Build.props), tested on compilations of their own
        [AnalyzersTest] = [Analyzers],
        [IntegrationTest] = null,
        [ArchitectureTest] = null,
    };

    private static void AssertNone(List<string> violations, string why)
        => Assert.That(violations, Is.Empty, () => why + ":\n" + string.Join("\n", violations));

    [Test]
    public void EveryTestProject_IsKnown()
    {
        var tests = ProjectFiles.All().Where(ProjectFiles.IsTest).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tests, Does.Contain(DomainTest), "the repository's projects are read");
            AssertNone(tests.Where(t => !Known.ContainsKey(t)).ToList(),
                "A test project is classified in TestProjectTests.Known, with the projects it may reference");
        }
    }

    [TestCase(DomainTest)]
    [TestCase(ApplicationTest)]
    [TestCase(InfrastructureTest)]
    [TestCase(ServerTest)]
    [TestCase(SeederTest)]
    [TestCase(ConvertTest)]
    [TestCase(AnalyzersTest)]
    public void AUnitTestProject_ReferencesItsLayerAndTheLayersBelow(string project)
    {
        AssertNone(ProjectFiles.Violations(project, Known[project]!),
            $"{project} references {string.Join(", ", Known[project]!)} alone");
    }

    [Test]
    public void IntegrationTests_ReferenceAnyProject_ButTheArchitectureTests()
    {
        AssertNone(ProjectFiles.Violations(IntegrationTest, ProjectFiles.All().Where(p => p != ArchitectureTest).ToArray()),
            $"{IntegrationTest} references the production projects and the unit test projects, never {ArchitectureTest}");
    }

    [Test]
    public void ArchitectureTests_ReferenceProductionProjectsOnly()
    {
        AssertNone(ProjectFiles.Violations(ArchitectureTest, ProjectFiles.All().Where(p => !ProjectFiles.IsTest(p)).ToArray()),
            $"{ArchitectureTest} reads the production projects' assemblies, and references no test project");
    }

    [Test]
    public void NoProject_ReferencesTheIntegrationOrTheArchitectureTests()
    {
        var references = ProjectFiles.All()
            .SelectMany(p => ProjectFiles.References(p).Where(r => r is IntegrationTest or ArchitectureTest).Select(r => $"{p}.csproj -> {r}"))
            .ToList();

        AssertNone(references, $"{IntegrationTest} and {ArchitectureTest} are leaves: no project references them");
    }
}
