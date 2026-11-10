using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// The Domain's bounded contexts (its namespaces GrifballWebApp.Domain.League, .LateLeague, .Archive, .Players, .Halo and
/// .Shared) keep their navigations to each other, by the owner's decision, but no new one comes in unseen: a type of one
/// context may name a type of another only where <see cref="ContextCrossings.Allowed"/> lists it.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BoundedContextTests
{
    private static readonly string DomainNamespace = typeof(Season).Assembly.GetName().Name! + ".";

    /// <summary>The bounded context a Domain type is in: the namespace segment after GrifballWebApp.Domain, or null.</summary>
    private static string? ContextOf(string @namespace)
    {
        if (!@namespace.StartsWith(DomainNamespace, StringComparison.Ordinal))
            return null;
        var rest = @namespace[DomainNamespace.Length..];
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest[..dot];
    }

    /// <summary>Every reference a Domain type makes to a type of another bounded context: (from, to, the type it names).</summary>
    private static List<(string From, string To, string Type, string By)> Crossings()
    {
        using var module = Dependencies.ModuleOf(typeof(Season));
        var own = module.Assembly.Name.Name;
        return Dependencies.Of(module)
            .Where(d => d.ToAssembly == own)
            .Select(d => (From: ContextOf(d.From.Namespace), To: ContextOf(d.To.Namespace), d.To.Name, By: d.From.FullName))
            .Where(c => c.From is not null && c.To is not null && c.From != c.To)
            .Select(c => (c.From!, c.To!, c.Name, c.By))
            .ToList();
    }

    [Test]
    public void TheContexts_AreTheDomainsNamespaces()
    {
        using var module = Dependencies.ModuleOf(typeof(Season));
        var contexts = module.Types.Select(t => ContextOf(t.Namespace)).OfType<string>().Distinct();

        Assert.That(contexts, Is.EquivalentTo(ContextCrossings.Contexts),
            "a new bounded context is listed in ContextCrossings.Contexts (and its crossings in ContextCrossings.Allowed)");
    }

    [Test]
    public void AContext_NamesAnotherContextsType_OnlyWhereAllowed()
    {
        var crossings = Crossings();
        var unlisted = crossings
            .Where(c => !ContextCrossings.Allows(c.From, c.To, c.Type))
            .Select(c => $"{c.From} -> {c.To}: {c.Type} (in {c.By})")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(crossings, Is.Not.Empty, "the scan finds today's crossings (a Season's matches' teams, ...)");
            Assert.That(unlisted, Is.Empty, () => "A type of one bounded context names another's. Reach it through its own context, or, if it belongs, list it in "
                + "ContextCrossings.Allowed:\n" + string.Join("\n", unlisted));
        }
    }

    [Test]
    public void EveryAllowedCrossing_IsStillMade()
    {
        var made = Crossings().Select(c => (c.From, c.To, c.Type)).ToHashSet();
        var stale = ContextCrossings.Allowed
            .SelectMany(from => from.Value.SelectMany(to => to.Value.Select(type => (From: from.Key, To: to.Key, Type: type))))
            .Where(c => !made.Contains(c))
            .Select(c => $"{c.From} -> {c.To}: {c.Type}");

        Assert.That(stale, Is.Empty, "a crossing no type makes any more comes off ContextCrossings.Allowed, so it can't come back unseen");
    }

    [Test]
    public void ACrossing_IsAllowedOnlyAsListed()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ContextCrossings.Allows("League", "League", "Anything"), Is.True, "a context's own types are its own");
            Assert.That(ContextCrossings.Allows("Halo", "League", "NoSuchType"), Is.False);
            Assert.That(ContextCrossings.Allows("NoSuchContext", "League", "Season"), Is.False);
            Assert.That(ContextOf("GrifballWebApp.Domain.League.Configurations"), Is.EqualTo("League"));
            Assert.That(ContextOf("GrifballWebApp.Domain"), Is.Null);
            Assert.That(ContextOf("System"), Is.Null);
        }
    }
}
