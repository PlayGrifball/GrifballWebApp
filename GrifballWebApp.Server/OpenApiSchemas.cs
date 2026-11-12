using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server;

/// <summary>
/// The OpenAPI document's schema names (Program.cs, CustomSchemaIds), which become the generated client's type names:
/// a type's own name, with its namespace's last segment in front only when another of the app's types has the same name
/// (TeamPage's and Teams' PlayerDto), and a generic type's arguments after "Of" (PaginationResultOfUserMergeDto).
/// </summary>
public static class OpenApiSchemaIds
{
    private const string AppPrefix = "GrifballWebApp";

    private static readonly Lazy<HashSet<string>> AppAmbiguousNames = new(() => AmbiguousNames(AppAssemblies()));

    /// <summary>The schema name of <paramref name="type"/>, among the app's own types.</summary>
    public static string For(Type type) => For(type, AppAmbiguousNames.Value);

    /// <summary>The schema name of <paramref name="type"/>, qualified when its name is one of <paramref name="ambiguous"/>.</summary>
    public static string For(Type type, IReadOnlySet<string> ambiguous)
    {
        if (type.IsArray)
            return $"ArrayOf{For(type.GetElementType()!, ambiguous)}";

        var name = NameOf(type);
        if (ambiguous.Contains(name))
        {
            var ns = $"{type.Namespace}"; // none: a type outside any namespace keeps its name
            name = ns[(ns.LastIndexOf('.') + 1)..] + name;
        }

        return type.IsGenericType
            ? $"{name}Of{string.Join("And", type.GetGenericArguments().Select(a => For(a, ambiguous)))}"
            : name;
    }

    /// <summary>
    /// The names more than one of these assemblies' public types share: only a public type can be a schema, so an internal
    /// helper never renames a DTO. Two schemas whose names and namespaces' last segments both match make Swashbuckle
    /// refuse the document, which the startup test (it fetches the document) fails on.
    /// </summary>
    public static HashSet<string> AmbiguousNames(IEnumerable<Assembly> assemblies)
        => assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t.IsVisible && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .GroupBy(NameOf)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The server and every one of the app's assemblies it references, directly or not.</summary>
    public static List<Assembly> AppAssemblies()
    {
        var found = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Stack<Assembly>([typeof(OpenApiSchemaIds).Assembly]);
        while (pending.TryPop(out var assembly))
        {
            if (!found.TryAdd(assembly.GetName().Name!, assembly))
                continue;
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name!.StartsWith(AppPrefix, StringComparison.Ordinal) && !found.ContainsKey(reference.Name))
                    pending.Push(Assembly.Load(reference));
            }
        }
        return [.. found.Values];
    }

    /// <summary>A type's name without its generic arity, its declaring types' names in front ("Outer" + "Inner").</summary>
    private static string NameOf(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];
        return type.IsNested ? NameOf(type.DeclaringType!) + name : name;
    }
}

/// <summary>
/// Marks every property of an object schema required (Program.cs): the API writes each property, null or not
/// (<see cref="ApiJson"/> ignores none), so the generated client's types need no optional properties. Nullability still
/// comes from the types (SupportNonNullableReferenceTypes). A property left out when null
/// (<c>[JsonIgnore(Condition = WhenWritingNull)]</c>) or default stays optional. Properties are named as the API's
/// serializer names them (its data contract, the one Swashbuckle builds the schema from).
/// </summary>
public sealed class EveryPropertyRequiredFilter(ISerializerDataContractResolver contracts) : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema { Properties.Count: > 0 } objectSchema)
            return;

        var sometimesLeftOut = (contracts.GetDataContractForType(context.Type).ObjectProperties ?? [])
            .Where(p => p.MemberInfo?.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: not JsonIgnoreCondition.Never })
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        objectSchema.Required = new SortedSet<string>(
            objectSchema.Properties.Keys.Where(k => !sometimesLeftOut.Contains(k)), StringComparer.Ordinal);
    }
}

/// <summary>
/// Describes requests and responses as application/json only, where it is one of their media types (Program.cs). ASP.NET
/// also accepts text/json and application/*+json and offers text/plain and text/json, which the client never uses; one
/// media type gives the generated client one method per operation. A form upload, problem details and text alone stay.
/// </summary>
public sealed class JsonOnlyOperationFilter : IOperationFilter
{
    private const string Json = "application/json";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        KeepJsonOnly(operation.RequestBody?.Content);
        if (operation.Responses is not { } responses)
            return;
        foreach (var response in responses.Values)
            KeepJsonOnly(response.Content);
    }

    private static void KeepJsonOnly<T>(IDictionary<string, T>? content)
    {
        if (content is null || !content.ContainsKey(Json))
            return;
        foreach (var type in content.Keys.Where(k => k != Json).ToList())
            content.Remove(type);
    }
}

/// <summary>
/// Names query parameters as the API's JSON names its properties (its naming policy, camelCase) (Program.cs): an action
/// that binds its query to a class (a list's paging and filters) would otherwise list them by property name, PascalCase,
/// and the generated client would send them so. Binding ignores case either way; the URLs stay the ones the client always
/// sent (and index.html fetches early).
/// </summary>
public sealed class JsonNamedQueryParametersFilter(IOptions<JsonOptions> json) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (json.Value.JsonSerializerOptions.PropertyNamingPolicy is not { } naming)
            return;
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parameter.In == ParameterLocation.Query && parameter.Name is { Length: > 0 } name)
                parameter.Name = naming.ConvertName(name);
        }
    }
}
