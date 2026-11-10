using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GrifballWebApp.Architecture.Test;

/// <summary>A type's use of another type: <see cref="From"/> names <see cref="To"/> somewhere in its compiled code.</summary>
/// <param name="From">The type that makes the reference (its outermost type: a lambda's closure is its method's type's).</param>
/// <param name="To">The type referenced (its outermost type, generic arguments, arrays and pointers taken apart).</param>
/// <param name="ToAssembly">The assembly <see cref="To"/> is in.</param>
internal sealed record Dependency(TypeDefinition From, TypeReference To, string ToAssembly)
{
    public override string ToString() => $"{From.FullName} -> {To.FullName} ({ToAssembly})";
}

/// <summary>
/// Every type a compiled assembly references, read with Mono.Cecil: the signatures (base types, interfaces, fields,
/// properties, events, methods, generic constraints, attributes) and the IL of every method body (the types, methods
/// and fields its instructions name, its locals). This is what the assembly depends on: the compiler leaves out an
/// assembly reference nothing uses, and a <c>using</c> alone compiles to nothing.
/// </summary>
internal static class Dependencies
{
    /// <summary>The module of the assembly <paramref name="type"/> is in, read from beside this one.</summary>
    public static ModuleDefinition ModuleOf(Type type) => ModuleDefinition.ReadModule(type.Assembly.Location);

    /// <summary>Every reference a type of <paramref name="module"/> makes to a type outside itself.</summary>
    public static List<Dependency> Of(ModuleDefinition module)
    {
        var assembly = module.Assembly.Name.Name;
        var found = new List<Dependency>();
        // The assembly's and the module's own attributes ([assembly: ...], [module: ...]), made by <Module>, the type of
        // what is in no type
        var assemblyLevel = Attributes(module.Assembly).Concat(Attributes(module)).ToList();
        foreach (var type in module.GetTypes())
        {
            var from = Outermost(type);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var referenced = type.Name == "<Module>" && type.DeclaringType is null ? Referenced(type).Concat(assemblyLevel) : Referenced(type);
            foreach (var reference in referenced.SelectMany(Flatten))
            {
                if (reference is GenericParameter)
                    continue;
                var to = OutermostReference(reference);
                var toAssembly = AssemblyName(to) ?? assembly;
                if (to.FullName == from.FullName || !seen.Add(to.FullName + "|" + toAssembly))
                    continue;
                found.Add(new Dependency(from, to, toAssembly));
            }
        }
        return found;
    }

    /// <summary>The outermost type of a nested type (the compiler's closures and state machines are nested in theirs).</summary>
    public static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }

    private static TypeReference OutermostReference(TypeReference type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }

    /// <summary>The assembly a reference's type is in: null for one in the module itself.</summary>
    private static string? AssemblyName(TypeReference type) => type.Scope switch
    {
        AssemblyNameReference name => name.Name,
        ModuleDefinition module => module.Assembly.Name.Name,
        _ => null,
    };

    /// <summary>A constructed type's parts: <c>List&lt;Season&gt;</c> is List and Season, <c>Season[]</c> is Season.</summary>
    private static IEnumerable<TypeReference> Flatten(TypeReference? type)
    {
        switch (type)
        {
            case null:
                yield break;
            case GenericInstanceType generic:
                foreach (var part in Flatten(generic.ElementType).Concat(generic.GenericArguments.SelectMany(Flatten)))
                    yield return part;
                yield break;
            case TypeSpecification specification: // arrays, by-refs, pointers, modifiers
                foreach (var part in Flatten(specification.ElementType))
                    yield return part;
                yield break;
            default:
                yield return type;
                yield break;
        }
    }

    private static IEnumerable<TypeReference> Referenced(TypeDefinition type)
    {
        yield return type.BaseType!;
        foreach (var implementation in type.Interfaces)
        {
            yield return implementation.InterfaceType;
            foreach (var item in Attributes(implementation))
                yield return item;
        }
        foreach (var item in Constraints(type.GenericParameters).Concat(Attributes(type)))
            yield return item;
        foreach (var field in type.Fields)
        {
            yield return field.FieldType;
            foreach (var item in Attributes(field))
                yield return item;
        }
        foreach (var property in type.Properties)
        {
            yield return property.PropertyType;
            foreach (var item in Attributes(property))
                yield return item;
        }
        foreach (var @event in type.Events)
        {
            yield return @event.EventType;
            foreach (var item in Attributes(@event))
                yield return item;
        }
        foreach (var method in type.Methods)
        foreach (var item in Referenced(method))
            yield return item;
    }

    private static IEnumerable<TypeReference> Referenced(MethodDefinition method)
    {
        yield return method.ReturnType;
        foreach (var parameter in method.Parameters)
        {
            yield return parameter.ParameterType;
            foreach (var item in Attributes(parameter))
                yield return item;
        }
        foreach (var item in Constraints(method.GenericParameters).Concat(Attributes(method)).Concat(Attributes(method.MethodReturnType)))
            yield return item;
        if (!method.HasBody)
            yield break;
        foreach (var local in method.Body.Variables)
            yield return local.VariableType;
        foreach (var handler in method.Body.ExceptionHandlers.Where(h => h.CatchType is not null))
            yield return handler.CatchType;
        foreach (var item in method.Body.Instructions.SelectMany(Operand))
            yield return item;
    }

    /// <summary>The types an instruction names: a type (typeof, a cast, newarr), a method's or a field's type and signature.</summary>
    private static IEnumerable<TypeReference> Operand(Instruction instruction)
    {
        switch (instruction.Operand)
        {
            case TypeReference type:
                yield return type;
                break;
            case MethodReference method:
                foreach (var item in Signature(method))
                    yield return item;
                break;
            case FieldReference field:
                yield return field.DeclaringType;
                yield return field.FieldType;
                break;
        }
    }

    private static IEnumerable<TypeReference> Signature(MethodReference method)
    {
        yield return method.DeclaringType;
        yield return method.ReturnType;
        foreach (var parameter in method.Parameters)
            yield return parameter.ParameterType;
        if (method is GenericInstanceMethod generic)
            foreach (var argument in generic.GenericArguments)
                yield return argument;
    }

    /// <summary>Generic parameters' attributes and constraints, and the constraints' own attributes.</summary>
    private static IEnumerable<TypeReference> Constraints(IEnumerable<GenericParameter> parameters)
        => parameters.SelectMany(p => Attributes(p).Concat(p.Constraints.SelectMany(c => Attributes(c).Prepend(c.ConstraintType))));

    /// <summary>An attribute's type and the types it names (<c>[AggregatePart(typeof(Season))]</c> names Season).</summary>
    private static IEnumerable<TypeReference> Attributes(ICustomAttributeProvider provider)
    {
        foreach (var attribute in provider.CustomAttributes)
        {
            yield return attribute.AttributeType;
            var arguments = attribute.ConstructorArguments
                .Concat(attribute.Fields.Select(f => f.Argument))
                .Concat(attribute.Properties.Select(p => p.Argument));
            foreach (var named in arguments.SelectMany(Named))
                yield return named;
        }
    }

    /// <summary>
    /// The types an attribute argument names: its own type (an enum's), a typeof value, and those of a value boxed in an
    /// object parameter (<c>[Attr((object)typeof(Season))]</c>) or in an array of them, however deep.
    /// </summary>
    private static IEnumerable<TypeReference> Named(CustomAttributeArgument argument)
    {
        yield return argument.Type;
        var inner = argument.Value switch
        {
            TypeReference named => [named],
            CustomAttributeArgument boxed => Named(boxed),
            CustomAttributeArgument[] array => array.SelectMany(Named),
            _ => Enumerable.Empty<TypeReference>(),
        };
        foreach (var named in inner)
            yield return named;
    }
}
