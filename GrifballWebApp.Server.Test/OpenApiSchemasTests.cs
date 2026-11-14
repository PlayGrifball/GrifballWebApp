using GrifballWebApp.Application.Dtos;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.Test;

/// <summary>A type with a nested one, for its schema name.</summary>
public sealed class Outer
{
    public sealed class Inner;
}

/// <summary>A DTO for the schema names and the required properties.</summary>
public sealed class Sample
{
    public int Id { get; set; }
    public string? Name { get; set; }
    [JsonPropertyName("renamed")]
    public string Other { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Score { get; set; }
}

/// <summary>What Swashbuckle builds its filters with: the API's JSON options and its serializer contract.</summary>
internal static class FilterServices
{
    /// <summary>The API's JSON options: ASP.NET's web defaults (camelCase).</summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static IServiceProvider For(JsonSerializerOptions json)
    {
        var services = new ServiceCollection();
        services.Configure<JsonOptions>(o => o.JsonSerializerOptions.PropertyNamingPolicy = json.PropertyNamingPolicy);
        services.AddSingleton<ISerializerDataContractResolver>(new JsonSerializerDataContractResolver(json));
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// The OpenAPI document's schema names and required properties (OpenApiSchemas.cs), which the client's generated types
/// are made from.
/// </summary>
[TestFixture]
public class OpenApiSchemasTests
{
    private static readonly HashSet<string> NoneAmbiguous = [];
    private static readonly string[] SampleRequired = ["id", "name", "renamed"];
    private static readonly string[] PascalRequired = ["Id", "Name", "renamed"];


    [Test]
    public void For_NamesATypeByItsOwnName()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(OpenApiSchemaIds.For(typeof(Sample), NoneAmbiguous), Is.EqualTo("Sample"));
            Assert.That(OpenApiSchemaIds.For(typeof(Outer.Inner), NoneAmbiguous), Is.EqualTo("OuterInner"));
        }
    }

    [Test]
    public void For_PutsTheNamespacesLastSegmentInFront_OnlyOfAnAmbiguousName()
    {
        var ambiguous = new HashSet<string> { "Sample" };

        Assert.That(OpenApiSchemaIds.For(typeof(Sample), ambiguous), Is.EqualTo("TestSample"));
    }

    [Test]
    public void For_AmongTheAppsOwnTypes_QualifiesOnlyANameTwoOfThemShare()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(OpenApiSchemaIds.For(typeof(GrifballWebApp.Application.Dtos.MatchDto)), Is.EqualTo("DtosMatchDto"));
            Assert.That(OpenApiSchemaIds.For(typeof(GrifballWebApp.Application.TeamPage.MatchDto)), Is.EqualTo("TeamPageMatchDto"));
            Assert.That(OpenApiSchemaIds.For(typeof(GrifballWebApp.Application.TeamPage.TeamPageService)), Is.EqualTo("TeamPageService"));
        }
    }

    [Test]
    public void For_NamesAGenericTypeByItsArguments()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(OpenApiSchemaIds.For(typeof(PaginationResult<Sample>), NoneAmbiguous), Is.EqualTo("PaginationResultOfSample"));
            Assert.That(OpenApiSchemaIds.For(typeof(Dictionary<string, Sample[]>), NoneAmbiguous),
                Is.EqualTo("DictionaryOfStringAndArrayOfSample"));
        }
    }

    [Test]
    public void AmbiguousNames_AreTheAppsDtoNamesSharedByTwoTypes()
    {
        var ambiguous = OpenApiSchemaIds.AmbiguousNames(OpenApiSchemaIds.AppAssemblies());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ambiguous, Does.Contain("TimeslotDto").And.Contain("MatchDto").And.Contain("PlayerDto"));
            Assert.That(ambiguous, Does.Not.Contain("SeasonListItemDto"));
            Assert.That(OpenApiSchemaIds.AppAssemblies().Select(a => a.GetName().Name),
                Does.Contain("GrifballWebApp.Server").And.Contain("GrifballWebApp.Application"));
        }
    }

    [Test]
    public void AmbiguousNames_CountOnlyTypesTheApiCouldSend()
    {
        // A non-public type is never a schema: an internal helper of the same name must not rename the public DTO's schema
        var ambiguous = OpenApiSchemaIds.AmbiguousNames([typeof(OpenApiSchemasTests).Assembly]);

        Assert.That(ambiguous, Does.Not.Contain("HiddenTwin"));
    }

    [Test]
    public void EveryPropertyRequired_NamesPropertiesAsTheSerializerDoes()
    {
        // The API's own naming (here none: PascalCase), not camelCase assumed: Score is left out when null, so stays optional
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Id"] = new OpenApiSchema(),
                ["Name"] = new OpenApiSchema(),
                ["renamed"] = new OpenApiSchema(),
                ["Score"] = new OpenApiSchema(),
            },
        };
        var filter = ActivatorUtilities.CreateInstance<EveryPropertyRequiredFilter>(FilterServices.For(new JsonSerializerOptions()));

        filter.Apply(schema, Context(typeof(Sample)));

        Assert.That(schema.Required, Is.EqualTo(PascalRequired));
    }

    [Test]
    public void EveryPropertyRequired_SkipsOnlyOnesLeftOutWhenNull()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema(),
                ["name"] = new OpenApiSchema(),
                ["renamed"] = new OpenApiSchema(),
                ["score"] = new OpenApiSchema(),
            },
        };

        ActivatorUtilities.CreateInstance<EveryPropertyRequiredFilter>(FilterServices.For(FilterServices.Web)).Apply(schema, Context(typeof(Sample)));

        Assert.That(schema.Required, Is.EqualTo(SampleRequired));
    }

    [Test]
    public void EveryPropertyRequired_LeavesASchemaWithoutPropertiesAlone()
    {
        var schema = new OpenApiSchema();

        ActivatorUtilities.CreateInstance<EveryPropertyRequiredFilter>(FilterServices.For(FilterServices.Web)).Apply(schema, Context(typeof(int)));

        Assert.That(schema.Required, Is.Null);
    }

    private static SchemaFilterContext Context(Type type)
        => new(type, null!, new SchemaRepository(), null!, null!);
}

/// <summary>
/// Requests and responses described as application/json only, where it is one of their media types (OpenApiSchemas.cs):
/// the client sends and reads JSON, and one media type gives the generated client one method per operation.
/// </summary>
[TestFixture]
public class JsonOnlyOperationFilterTests
{
    private static readonly string[] Json = ["application/json"];
    private static readonly string[] Problem = ["application/problem+json"];
    private static readonly string[] Plain = ["text/plain"];
    private static readonly string[] Form = ["multipart/form-data"];

    private static Dictionary<string, OpenApiMediaType> Content(params string[] types)
        => types.ToDictionary(t => t, _ => new OpenApiMediaType());

    [Test]
    public void Apply_KeepsOnlyApplicationJson_WhereItIsOffered()
    {
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody { Content = Content("application/json", "text/json", "application/*+json") },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Content = Content("text/plain", "application/json", "text/json") },
                ["400"] = new OpenApiResponse { Content = Content("application/problem+json") },
                ["201"] = new OpenApiResponse { Content = Content("text/plain") },
            },
        };

        new JsonOnlyOperationFilter().Apply(operation, null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(operation.RequestBody!.Content!.Keys, Is.EqualTo(Json));
            Assert.That(operation.Responses["200"].Content!.Keys, Is.EqualTo(Json));
            Assert.That(operation.Responses["400"].Content!.Keys, Is.EqualTo(Problem), "Problem details stay");
            Assert.That(operation.Responses["201"].Content!.Keys, Is.EqualTo(Plain), "Text alone stays");
        }
    }

    [Test]
    public void Apply_LeavesAFormUploadAndAnOperationWithoutBodiesAlone()
    {
        var upload = new OpenApiOperation { RequestBody = new OpenApiRequestBody { Content = Content("multipart/form-data") } };
        var bare = new OpenApiOperation { Responses = null };

        new JsonOnlyOperationFilter().Apply(upload, null!);
        new JsonOnlyOperationFilter().Apply(bare, null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(upload.RequestBody!.Content!.Keys, Is.EqualTo(Form));
            Assert.That(bare.RequestBody, Is.Null);
        }
    }
}

/// <summary>Query parameters named camelCase, as the API's JSON names properties (OpenApiSchemas.cs).</summary>
[TestFixture]
public class JsonNamedQueryParametersFilterTests
{
    private static readonly string[] QueryNames = ["pageNumber", "q", "SeasonID"];

    [Test]
    public void Apply_NamesQueryParametersCamelCase_AndLeavesTheRouteAlone()
    {
        var operation = new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter { Name = "PageNumber", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "q", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "SeasonID", In = ParameterLocation.Path },
            ],
        };

        ActivatorUtilities.CreateInstance<JsonNamedQueryParametersFilter>(FilterServices.For(FilterServices.Web)).Apply(operation, null!);

        Assert.That(operation.Parameters!.Select(p => p.Name), Is.EqualTo(QueryNames));
    }

    [Test]
    public void Apply_NamesQueryParametersAsTheApisJsonDoes()
    {
        // No naming policy: the JSON keeps .NET's names, so the query does too
        var operation = new OpenApiOperation { Parameters = [new OpenApiParameter { Name = "PageNumber", In = ParameterLocation.Query }] };
        var filter = ActivatorUtilities.CreateInstance<JsonNamedQueryParametersFilter>(FilterServices.For(new JsonSerializerOptions()));

        filter.Apply(operation, null!);

        Assert.That(operation.Parameters![0].Name, Is.EqualTo("PageNumber"));
    }

    [Test]
    public void Apply_LeavesAnOperationWithoutParametersAlone()
    {
        var operation = new OpenApiOperation { Parameters = null };

        ActivatorUtilities.CreateInstance<JsonNamedQueryParametersFilter>(FilterServices.For(FilterServices.Web)).Apply(operation, null!);

        Assert.That(operation.Parameters, Is.Null);
    }
}

/// <summary>A hub of every kind of method, for its payloads: a value, a Task's result, nothing.</summary>
[TypedSignalR.Client.Hub]
public interface ISampleHub
{
    int Count();
    List<int> All();
    Task<string> Name();
    Task Save(Sample sample);
}

/// <summary>A base hub interface: its methods are the hub's too.</summary>
public interface IBaseSampleHub
{
    Task<Outer.Inner> Inherited();
}

/// <summary>A hub of the other kinds of method: ValueTask, streams either way, and inherited ones.</summary>
[TypedSignalR.Client.Hub]
public interface IStreamingSampleHub : IBaseSampleHub
{
    ValueTask<Sample> One();
    IAsyncEnumerable<Outer> Stream();
    System.Threading.Channels.ChannelReader<double> Channel();
    Task Upload(IAsyncEnumerable<decimal> items);
    Task<System.Threading.Channels.ChannelReader<Guid>> DoubleWrapped();
}

/// <summary>The hubs' payloads in the document (OpenApiSchemas.cs), for the client's models and its typed SignalR client.</summary>
[TestFixture]
public class HubSchemasDocumentFilterTests
{
    private static readonly Type[] SamplePayloads = [typeof(int), typeof(List<int>), typeof(string), typeof(Sample)];
    private static readonly Type[] StreamedPayloads = [typeof(Sample), typeof(Outer), typeof(double), typeof(decimal), typeof(Outer.Inner), typeof(Guid)];
    private static readonly Type[] Wrappers =
    [
        typeof(ValueTask<Sample>), typeof(IAsyncEnumerable<Outer>), typeof(System.Threading.Channels.ChannelReader<double>),
        typeof(IAsyncEnumerable<decimal>), typeof(Task<Outer.Inner>),
        // Doubly wrapped: unwrapping once still leaves a wrapper (ChannelReader<Guid>), which must be unwrapped too
        typeof(Task<System.Threading.Channels.ChannelReader<Guid>>), typeof(System.Threading.Channels.ChannelReader<Guid>),
    ];

    [Test]
    public void PayloadTypes_AreWhatTheHubsTakeAnswerAndSend()
    {
        var types = HubSchemasDocumentFilter.PayloadTypes(typeof(HubSchemasDocumentFilter).Assembly);

        using (Assert.EnterMultipleScope())
        {
            // Sent to the receivers
            Assert.That(types, Does.Contain(typeof(GrifballWebApp.Application.Teams.CaptainAddedDto)));
            Assert.That(types, Does.Contain(typeof(GrifballWebApp.Application.Teams.MovePlayerToTeamRequestDto)));
            // Taken and answered by the hub's methods
            Assert.That(types, Does.Contain(typeof(GrifballWebApp.Application.Teams.CaptainPlacementDto)));
            Assert.That(types, Does.Contain(typeof(List<GrifballWebApp.Application.Teams.TeamResponseDto>)));
            // Not what a method returns when it returns nothing
            Assert.That(types, Does.Not.Contain(typeof(Task)).And.Not.Contain(typeof(void)));
        }
    }

    [Test]
    public void PayloadTypes_AreWhatAValueTaskOrAStreamCarries_AndAnInheritedMethodsToo()
    {
        var types = HubSchemasDocumentFilter.PayloadTypes(typeof(IStreamingSampleHub).Assembly);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(types, Is.SupersetOf(StreamedPayloads));
            Assert.That(types, Has.None.AnyOf(Wrappers));
        }
    }

    [Test]
    public void PayloadTypes_AreAValueATasksResultAndAnArgument()
    {
        var types = HubSchemasDocumentFilter.PayloadTypes(typeof(ISampleHub).Assembly);

        Assert.That(types, Is.SupersetOf(SamplePayloads).And.Not.Contain(typeof(void)));
    }
}

/// <summary>The OpenAPI document's endpoints (OpenApiSchemas.cs): the API's, not the hubs' page's.</summary>
[TestFixture]
public class OpenApiInclusionTests
{
    private static Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription Api(string path, string? group = null)
        => new() { RelativePath = path, GroupName = group };

    [Test]
    public void Includes_TheApi_NotTheHubsPage()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(OpenApiInclusion.Includes("v1", Api("seasons/{seasonID}")), Is.True);
            Assert.That(OpenApiInclusion.Includes("v1", Api("seasons", group: "v1")), Is.True);
            Assert.That(OpenApiInclusion.Includes("v1", Api("seasons", group: "v2")), Is.False);
            Assert.That(OpenApiInclusion.Includes("v1", Api("signalr-dev/spec.json")), Is.False);
            Assert.That(OpenApiInclusion.Includes("v1", Api("signalr-dev/index.html")), Is.False);
            Assert.That(OpenApiInclusion.Includes("v1", new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription()), Is.True, "No route: not the page's");
        }
    }
}

/// <summary>An enum the API sends as its number: a converter of its own says so (as brackets-model's Status does).</summary>
[JsonConverter(typeof(JsonNumberEnumConverter<Level>))]
public enum Level { Low = 1, High = 5 }

/// <summary>An enum the API sends as its name (the global JsonStringEnumConverter).</summary>
public enum Colour { Red, Blue }

/// <summary>
/// An enum the API sends as its number is described so in the OpenAPI document (NumberEnumSchemaFilter), and every
/// property of one carries the converter itself, which is what makes it a number at runtime.
/// </summary>
[TestFixture]
public class NumberEnumSchemaFilterTests
{
    private static OpenApiSchema NamesSchema(params string[] names)
        => new() { Type = JsonSchemaType.String, Enum = [.. names.Select(n => (JsonNode)JsonValue.Create(n)!)] };

    private static SchemaFilterContext Context(Type type)
        => new(type, null!, new SchemaRepository(), null!, null!);

    [Test]
    public void DescribesAnEnumSentAsItsNumber_AsItsNumbers()
    {
        var schema = NamesSchema("Low", "High");

        new NumberEnumSchemaFilter().Apply(schema, Context(typeof(Level)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.Type, Is.EqualTo(JsonSchemaType.Integer));
            Assert.That(schema.Format, Is.EqualTo("int32"));
            Assert.That(schema.Enum!.Select(v => v.GetValue<int>()), Is.EqualTo(new[] { 1, 5 }));
        }
    }

    [Test]
    public void LeavesAnEnumSentAsItsName_AndAnyOtherType()
    {
        var colour = NamesSchema("Red", "Blue");
        var text = new OpenApiSchema { Type = JsonSchemaType.String };

        new NumberEnumSchemaFilter().Apply(colour, Context(typeof(Colour)));
        new NumberEnumSchemaFilter().Apply(text, Context(typeof(string)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(colour.Type, Is.EqualTo(JsonSchemaType.String));
            Assert.That(colour.Enum!.Select(v => v.GetValue<string>()), Is.EqualTo(new[] { "Red", "Blue" }));
            Assert.That(text.Type, Is.EqualTo(JsonSchemaType.String));
            Assert.That(text.Enum, Is.Null);
        }
    }

    [Test]
    public void BracketStatusIsSentAsItsNumber()
    {
        Assert.That(NumberEnumSchemaFilter.SentAsNumber(typeof(GrifballWebApp.Application.Brackets.Status)), Is.True);
    }

    [Test]
    public void EveryPropertyOfAnEnumSentAsItsNumber_CarriesTheConverterItself()
    {
        // The API's global JsonStringEnumConverter outranks a converter on the enum type: only one on the property makes it
        // a number, so a property without one would be sent as the name the document doesn't describe
        var missing = OpenApiSchemaIds.AppAssemblies()
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => NumberEnumSchemaFilter.SentAsNumber(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
            .Where(p => !NumberEnumSchemaFilter.IsNumberEnumConverter(p.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType))
            .Select(p => $"{p.DeclaringType!.FullName}.{p.Name}")
            .ToList();

        Assert.That(missing, Is.Empty);
    }
}
