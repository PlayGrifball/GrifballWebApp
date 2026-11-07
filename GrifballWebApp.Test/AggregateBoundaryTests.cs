using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GrifballWebApp.Test;

/// <summary>
/// An aggregate's internals are its own. The compiler keeps the app (another assembly) out of a model's internal
/// constructors, setters and methods, but not the rest of GrifballWebApp.Database: these tests do, reading its compiled
/// code. A model type's internal members may be used only by its aggregate (the root and the parts that name it with
/// <see cref="AggregatePartAttribute"/>), by code that names the aggregate with <see cref="AggregateAccessAttribute"/>, and
/// by EF (which sets them by reflection, never by a call). The tests reach in through InternalsVisibleTo, as they must.
/// And a part, never a root, has an internal DbSet: the app reaches it through its root.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AggregateBoundaryTests
{
    private static readonly string ModelsNamespace = typeof(MatchedMatch).Namespace!;

    private static ModuleDefinition Database() => ModuleDefinition.ReadModule(typeof(GrifballContext).Assembly.Location);

    /// <summary>The aggregate a model type is in: the root its <see cref="AggregatePartAttribute"/> names, or itself.</summary>
    private static string AggregateOf(TypeDefinition type)
    {
        var part = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == nameof(AggregatePartAttribute));
        return part is null ? type.FullName : ((TypeReference)part.ConstructorArguments[0].Value).FullName;
    }

    /// <summary>The aggregates <paramref name="provider"/>'s <see cref="AggregateAccessAttribute"/>s let it into.</summary>
    private static IEnumerable<string> AccessOf(Mono.Cecil.ICustomAttributeProvider provider)
        => provider.CustomAttributes.Where(a => a.AttributeType.Name == nameof(AggregateAccessAttribute))
            .Select(a => ((TypeReference)a.ConstructorArguments[0].Value).FullName);

    private static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }

    private static bool IsModel(TypeDefinition type) => Outermost(type).Namespace == ModelsNamespace;

    /// <summary>
    /// The method <paramref name="method"/> was written in: itself, or for a lambda or an iterator, which the compiler
    /// makes a method of its own ("&lt;RemoveArchivedMatch&gt;b__0"), the method named in its name.
    /// </summary>
    private static IEnumerable<MethodDefinition> WrittenIn(MethodDefinition method)
    {
        yield return method;
        foreach (var name in new[] { method.Name, method.DeclaringType.Name })
        {
            var end = name.IndexOf('>');
            if (!name.StartsWith('<') || end <= 1)
                continue;
            var source = name[1..end];
            foreach (var candidate in Outermost(method.DeclaringType).Methods.Where(m => m.Name == source))
                yield return candidate;
        }
    }

    /// <summary>Every use of a model type's internal member, with whether its caller may make it.</summary>
    private static IEnumerable<(string Use, bool Allowed)> InternalUses()
    {
        using var module = Database();
        foreach (var type in module.GetTypes())
        foreach (var method in type.Methods.Where(m => m.HasBody))
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.FlowControl != FlowControl.Call && instruction.OpCode.Code != Code.Newobj)
                continue;
            if (instruction.Operand is not MethodReference reference || reference.Resolve() is not { } target)
                continue;
            if (!(target.IsAssembly || target.IsFamilyOrAssembly) || !IsModel(target.DeclaringType))
                continue;

            var aggregate = AggregateOf(Outermost(target.DeclaringType));
            var caller = Outermost(type);
            var allowed = (IsModel(caller) && AggregateOf(caller) == aggregate)
                || AccessOf(caller).Contains(aggregate)
                || WrittenIn(method).Any(m => AccessOf(m).Contains(aggregate));
            yield return ($"{caller.FullName}.{method.Name} uses {target.DeclaringType.Name}.{target.Name} ({aggregate})", allowed);
        }
    }

    [Test]
    public void AModelsInternals_AreUsedOnlyByItsAggregate_OrCodeLetIn()
    {
        var uses = InternalUses().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(uses, Is.Not.Empty, "the scan finds the aggregates' own uses (MatchedMatch making its teams)");
            Assert.That(uses.Where(u => !u.Allowed).Select(u => u.Use), Is.Empty,
                $"Use the root's methods, or name the aggregate with [{nameof(AggregateAccessAttribute)}] where code outside it must reach in");
        });
    }

    /// <summary>The converted aggregates: every root (<see cref="AggregateRootAttribute"/>) and part.</summary>
    private static List<Type> AggregateTypes() => typeof(MatchedMatch).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<AggregateRootAttribute>() is not null || t.GetCustomAttribute<AggregatePartAttribute>() is not null)
        .ToList();

    // Milan Jovanović's rules for a domain's entities (Entities_ShouldOnlyHave_PrivateConstructors), as plain reflection:
    // a root is made by its factories, a part by its root
    [Test]
    public void AnAggregate_HasNoPublicConstructor()
    {
        var types = AggregateTypes();

        Assert.Multiple(() =>
        {
            Assert.That(types, Is.Not.Empty);
            foreach (var type in types)
                Assert.That(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public), Is.Empty, $"{type.Name}: made by its factories or its root");
        });
    }

    // And changed only by its methods: no property the app can set. Its own properties; the audit columns its base class
    // has (AuditableEntity) are the audit interceptor's.
    [Test]
    public void AnAggregate_HasNoPublicSetter()
    {
        var types = AggregateTypes();

        Assert.Multiple(() =>
        {
            foreach (var type in types)
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
                Assert.That(property.SetMethod?.IsPublic ?? false, Is.False, $"{type.Name}.{property.Name}: changed only by its methods");
        });
    }

    [Test]
    public void CodeLetIn_NamesTheAggregateItReachesInto()
    {
        var access = typeof(GrifballContext).GetMethod(nameof(GrifballContext.RemoveArchivedMatch))!.GetCustomAttribute<AggregateAccessAttribute>();

        Assert.That(access?.Root, Is.EqualTo(typeof(MatchedMatch)), "the context deletes an archive game through its root");
    }

    [Test]
    public void EveryPart_NamesARoot()
    {
        var parts = typeof(MatchedMatch).Assembly.GetTypes()
            .Select(t => (Type: t, Part: t.GetCustomAttribute<AggregatePartAttribute>()))
            .Where(p => p.Part is not null)
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var (type, part) in parts)
                Assert.That(part!.Root.GetCustomAttribute<AggregateRootAttribute>(), Is.Not.Null, $"{type.Name}'s root {part.Root.Name} is marked [AggregateRoot]");
        });
    }

    [Test]
    public void EveryPart_NamesARootThatIsAModel_AndNotAPartItself()
    {
        var parts = typeof(MatchedMatch).Assembly.GetTypes()
            .Select(t => (Type: t, Part: t.GetCustomAttribute<AggregatePartAttribute>()))
            .Where(p => p.Part is not null)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(parts, Is.Not.Empty);
            foreach (var (type, part) in parts)
            {
                Assert.That(part!.Root.Namespace, Is.EqualTo(ModelsNamespace), type.Name);
                Assert.That(part.Root.GetCustomAttribute<AggregatePartAttribute>(), Is.Null, $"{type.Name}'s root {part.Root.Name} is a part itself");
            }
        });
    }

    [Test]
    public void APartsDbSet_IsInternal_ARootsPublic()
    {
        var sets = typeof(GrifballContext).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
            .Select(p => (Property: p, Entity: p.PropertyType.GetGenericArguments()[0]))
            .Where(s => s.Entity.GetCustomAttribute<AggregatePartAttribute>() is not null)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sets, Is.Not.Empty);
            foreach (var (property, entity) in sets)
                Assert.That(property.GetMethod!.IsAssembly, Is.True, $"{property.Name}: {entity.Name} is part of an aggregate, so its DbSet is internal");
        });
    }
}
