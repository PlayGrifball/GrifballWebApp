using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
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
/// Describes an enum the API sends as its number as an integer enum (Program.cs): Swashbuckle describes every enum by its
/// names, as the API's global JsonStringEnumConverter writes them. An enum says it is sent as its number with a
/// <c>[JsonConverter(typeof(JsonNumberEnumConverter&lt;T&gt;))]</c> of its own (brackets-model's Status, ViewerData.cs);
/// that one doesn't change what is written (the global converter outranks a type's), so each property of it carries the
/// same converter, which does (OpenApiSchemasTests holds them to it).
/// </summary>
public sealed class NumberEnumSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Its own schema only: a property's (the filters see those too, with the property's type) refers to it
        if (schema is not OpenApiSchema enumSchema || context.MemberInfo is not null || !SentAsNumber(context.Type))
            return;
        enumSchema.Type = JsonSchemaType.Integer;
        enumSchema.Format = "int32";
        enumSchema.Enum = [.. Enum.GetValues(context.Type).Cast<object>().Select(v => (JsonNode)JsonValue.Create(Convert.ToInt32(v, CultureInfo.InvariantCulture)))];
    }

    /// <summary>An enum that says it is sent as its number: a JsonNumberEnumConverter of its own.</summary>
    public static bool SentAsNumber(Type type)
        => type.IsEnum && IsNumberEnumConverter(type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType);

    /// <summary>A <c>JsonNumberEnumConverter&lt;T&gt;</c>.</summary>
    public static bool IsNumberEnumConverter(Type? converter)
        => converter is { IsGenericType: true } && converter.GetGenericTypeDefinition() == typeof(JsonNumberEnumConverter<>);
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

/// <summary>
/// Adds the hubs' payloads to the document's schemas (Program.cs): what the hubs' methods take and answer and their
/// receivers are sent, so the client's models have them too (CaptainAddedDto is only ever sent over SignalR) and the
/// typed SignalR client (src/generated/signalr) uses the same models as the REST one.
/// </summary>
public sealed class HubSchemasDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var type in PayloadTypes(typeof(HubSchemasDocumentFilter).Assembly))
            context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository);
    }

    /// <summary>
    /// What the assembly's hub and receiver interfaces' methods (their base interfaces' too) take and answer: a task's
    /// result and what a stream carries, either way, not the wrapper.
    /// </summary>
    public static List<Type> PayloadTypes(Assembly assembly)
        => assembly.GetTypes()
            .Where(t => t.IsInterface && (t.IsDefined(typeof(TypedSignalR.Client.HubAttribute), false) || t.IsDefined(typeof(TypedSignalR.Client.ReceiverAttribute), false)))
            .SelectMany(t => t.GetMethods().Concat(t.GetInterfaces().SelectMany(i => i.GetMethods())))
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Select(Carried)
            .Where(t => t != typeof(void) && t != typeof(CancellationToken))
            .Distinct()
            .ToList();

    /// <summary>The wrappers a hub method's result or argument comes in, around what goes over the wire.</summary>
    private static readonly Type[] Wrappers = [typeof(Task<>), typeof(ValueTask<>), typeof(IAsyncEnumerable<>), typeof(System.Threading.Channels.ChannelReader<>)];

    /// <summary>What a method's parameter or result carries, unwrapping every layer (a <c>Task&lt;ChannelReader&lt;T&gt;&gt;</c>'s T).</summary>
    private static Type Carried(Type type)
    {
        while (type.IsGenericType && Wrappers.Contains(type.GetGenericTypeDefinition()))
            type = type.GetGenericArguments()[0];
        return type == typeof(Task) || type == typeof(ValueTask) ? typeof(void) : type;
    }
}

/// <summary>
/// Which endpoints the OpenAPI document describes (Program.cs, DocInclusionPredicate): Swashbuckle's default (an endpoint
/// of no group, or of the document's), without the hubs' page's own (TypedSignalR.Client.DevTools maps /signalr-dev/...
/// as minimal endpoints): it is a page, not the API, and the client would be generated a call for it.
/// </summary>
public static class OpenApiInclusion
{
    public static bool Includes(string documentName, Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription api)
        => (api.GroupName is null || api.GroupName == documentName)
           && api.RelativePath?.StartsWith("signalr-dev", StringComparison.OrdinalIgnoreCase) != true;
}
