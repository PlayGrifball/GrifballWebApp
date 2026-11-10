using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Seasons;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// The layers depend inwards only: Domain &lt;- Application &lt;- Infrastructure &lt;- Server. Each layer may use the
/// projects of this solution listed for it and no other (packages are its own business: the Domain references EF Core and
/// holds the entity configurations, by the owner's decision). Read from the compiled code, so a failure names each type
/// that crosses, and what it reaches, and from the project files (a const or nameof leaves nothing in the compiled code).
/// </summary>
/// <remarks>
/// A project reference the wrong way round between two layers is a cycle the build already refuses (Server references
/// Infrastructure, which references Application, which references Domain). What compiles is a new project reference to
/// one of the solution's other projects (DiscordInterfaces, the migrations, the Seeder) or a layer that drops a
/// reference it has today: these tests catch both.
/// </remarks>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LayerTests
{
    /// <summary>The solution's own projects: every assembly the layer rules are about.</summary>
    private static bool IsOurs(string assembly)
        => assembly.StartsWith("GrifballWebApp.", StringComparison.Ordinal) || assembly.StartsWith("DiscordInterface", StringComparison.Ordinal);

    private static readonly string Domain = typeof(Season).Assembly.GetName().Name!;
    private static readonly string Application = typeof(SeasonService).Assembly.GetName().Name!;
    private static readonly string Infrastructure = typeof(GrifballContext).Assembly.GetName().Name!;
    private static readonly string Server = typeof(SeasonController).Assembly.GetName().Name!;

    /// <summary>The references <paramref name="layer"/>'s types make to the solution's projects other than <paramref name="allowed"/>.</summary>
    private static List<string> Violations(Type layer, params string[] allowed)
    {
        using var module = Dependencies.ModuleOf(layer);
        var own = module.Assembly.Name.Name;
        return Dependencies.Of(module)
            .Where(d => d.ToAssembly != own && IsOurs(d.ToAssembly) && !allowed.Contains(d.ToAssembly))
            .Select(d => d.ToString())
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The solution's projects <paramref name="layer"/>'s types use.</summary>
    private static List<string> Uses(Type layer)
    {
        using var module = Dependencies.ModuleOf(layer);
        var own = module.Assembly.Name.Name;
        return Dependencies.Of(module).Select(d => d.ToAssembly).Where(a => a != own && IsOurs(a)).Distinct().Order(StringComparer.Ordinal).ToList();
    }

    private static void AssertNone(List<string> violations, string why)
        => Assert.That(violations, Is.Empty, () => why + ":\n" + string.Join("\n", violations));

    [Test]
    public void Domain_DependsOnNoOtherProject()
    {
        AssertNone(Violations(typeof(Season)),
            $"{Domain} is the core: it uses none of the solution's other projects (EF Core, a package, it may)");
    }

    [Test]
    public void Application_DependsOnlyOnDomain()
    {
        AssertNone(Violations(typeof(SeasonService), Domain),
            $"{Application} uses {Domain} alone: the outside world (database plumbing, Halo, Discord, the web) is reached through its ports, which {Infrastructure} implements");
    }

    [Test]
    public void Infrastructure_DependsOnApplicationAndDomain_NeverServer()
    {
        AssertNone(Violations(typeof(GrifballContext), Domain, Application, "DiscordInterfaces"),
            $"{Infrastructure} implements {Application}'s ports over {Domain} (and NetCord's DiscordInterfaces); {Server}, the migrations and the tools sit above it");
    }

    // The same rules on the project files: a use the compiler leaves no trace of (a const, nameof, an enum's value, all
    // inlined) needs a <ProjectReference>, so the reference itself is held to them too.

    [Test]
    public void Domain_ReferencesNoOtherProject()
    {
        AssertNone(ProjectFiles.Violations(Domain), $"{Domain}.csproj references none of the solution's projects");
    }

    [Test]
    public void Application_ReferencesOnlyDomain()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProjectFiles.References(Application), Does.Contain(Domain), "the csproj is read");
            AssertNone(ProjectFiles.Violations(Application, Domain), $"{Application}.csproj references {Domain} alone");
        }
    }

    [Test]
    public void Infrastructure_ReferencesOnlyApplicationAndDomain()
    {
        AssertNone(ProjectFiles.Violations(Infrastructure, Domain, Application, "DiscordInterfaces"),
            $"{Infrastructure}.csproj references {Application}, {Domain} and DiscordInterfaces alone");
    }

    [Test]
    public void Server_MayDependOnEveryLayer()
    {
        Assert.That(Uses(typeof(SeasonController)), Is.SupersetOf(new[] { Domain, Application, Infrastructure }),
            $"{Server} composes the layers: the scan sees its uses of each");
    }

    // The scan itself: these assemblies do cross, and each crossing names the type that makes it and the type it reaches
    [Test]
    public void AViolation_NamesTheTypeThatCrosses_AndWhatItReaches()
    {
        var violations = Violations(typeof(LayerTests));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(violations, Has.Some.EqualTo($"{typeof(LayerTests).FullName} -> {typeof(SeasonController).FullName} ({Server})"));
            Assert.That(violations, Has.Some.EqualTo($"{typeof(LayerTests).FullName} -> {typeof(GrifballContext).FullName} ({Infrastructure})"));
            Assert.That(Violations(typeof(GrifballContext), Domain), Has.Some.StartsWith($"{typeof(GrifballContext).FullName} -> ").And.Some.EndsWith($"({Application})"),
                "an Infrastructure type that uses Application, were Application not let in");
        }
    }
}
