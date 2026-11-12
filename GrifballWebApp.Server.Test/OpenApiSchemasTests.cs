using GrifballWebApp.Application.Dtos;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
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
        var bare = new OpenApiOperation();

        new JsonOnlyOperationFilter().Apply(upload, null!);
        new JsonOnlyOperationFilter().Apply(bare, null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(upload.RequestBody!.Content!.Keys, Is.EqualTo(Form));
            Assert.That(bare.RequestBody, Is.Null);
        }
    }
}
