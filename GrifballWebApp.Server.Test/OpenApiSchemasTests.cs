using GrifballWebApp.Application.Dtos;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
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

    /// <summary>What Swashbuckle's filters are built with: the API's JSON options and its serializer contract.</summary>
    internal static IServiceProvider Services(JsonSerializerOptions json)
    {
        var services = new ServiceCollection();
        services.Configure<JsonOptions>(o => o.JsonSerializerOptions.PropertyNamingPolicy = json.PropertyNamingPolicy);
        services.AddSingleton<ISerializerDataContractResolver>(new JsonSerializerDataContractResolver(json));
        return services.BuildServiceProvider();
    }

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
        var filter = ActivatorUtilities.CreateInstance<EveryPropertyRequiredFilter>(Services(new JsonSerializerOptions()));

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

        new EveryPropertyRequiredFilter().Apply(schema, Context(typeof(Sample)));

        Assert.That(schema.Required, Is.EqualTo(SampleRequired));
    }

    [Test]
    public void EveryPropertyRequired_LeavesASchemaWithoutPropertiesAlone()
    {
        var schema = new OpenApiSchema();

        new EveryPropertyRequiredFilter().Apply(schema, Context(typeof(int)));

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
public class CamelCaseQueryParametersFilterTests
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

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        Assert.That(operation.Parameters!.Select(p => p.Name), Is.EqualTo(QueryNames));
    }

    [Test]
    public void Apply_NamesQueryParametersAsTheApisJsonDoes()
    {
        // No naming policy: the JSON keeps .NET's names, so the query does too
        var operation = new OpenApiOperation { Parameters = [new OpenApiParameter { Name = "PageNumber", In = ParameterLocation.Query }] };
        var filter = ActivatorUtilities.CreateInstance<CamelCaseQueryParametersFilter>(OpenApiSchemasTests.Services(new JsonSerializerOptions()));

        filter.Apply(operation, null!);

        Assert.That(operation.Parameters![0].Name, Is.EqualTo("PageNumber"));
    }

    [Test]
    public void Apply_LeavesAnOperationWithoutParametersAlone()
    {
        var operation = new OpenApiOperation { Parameters = null };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        Assert.That(operation.Parameters, Is.Null);
    }
}
