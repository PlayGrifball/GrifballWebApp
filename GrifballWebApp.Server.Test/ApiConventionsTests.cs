using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The API's conventions (issue #270), checked on every action of the server's controllers, so a new one can't slip
/// past them: its authorization is explicit, and its refusals are ProblemDetails.
/// </summary>
[TestFixture]
public class ApiConventionsTests
{
    private static IEnumerable<MethodInfo> Actions() =>
        typeof(GrifballWebApp.Server.Seasons.SeasonController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            // Every action MVC would route: each public instance method a controller declares (not its property
            // accessors or a [NonAction]), whether or not it has an Http* attribute
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";

    // The XML doc comment ID the compiler writes for a member (ECMA-334 Appendix D), e.g.
    // "M:GrifballWebApp.Server.Seasons.SeasonController.GetSeasons(System.Threading.CancellationToken)": what
    // GenerateDocumentationFile's XML file keys its <member> entries by. Good enough for this codebase's actions,
    // which take no ref/out parameters or generic methods.
    private static string DocId(MethodInfo action)
    {
        var typeName = (action.DeclaringType!.FullName ?? action.DeclaringType!.Name).Replace('+', '.');
        var parameters = action.GetParameters();
        if (parameters.Length == 0)
            return $"M:{typeName}.{action.Name}";

        return $"M:{typeName}.{action.Name}({string.Join(",", parameters.Select(p => DocId(p.ParameterType)))})";
    }

    private static string DocId(Type type)
    {
        if (type.IsByRef)
            return DocId(type.GetElementType()!) + "@";
        if (type.IsArray)
            return DocId(type.GetElementType()!) + "[]";
        if (type.IsGenericType)
        {
            var definitionName = (type.GetGenericTypeDefinition().FullName ?? type.Name).Replace('+', '.');
            var tick = definitionName.IndexOf('`');
            if (tick >= 0)
                definitionName = definitionName[..tick];
            var args = string.Join(",", type.GetGenericArguments().Select(DocId));
            return $"{definitionName}{{{args}}}";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    [Test]
    public void EveryAction_HasAnHttpMethodAttribute()
    {
        // [HttpGet] or [HttpPost] with its route: not a bare [Route] or [AcceptVerbs], which the other rules here
        // wouldn't see
        var unmarked = Actions()
            .Where(a => !a.GetCustomAttributes<HttpMethodAttribute>().Any()
                || a.GetCustomAttributes<RouteAttribute>().Any()
                || a.GetCustomAttributes<AcceptVerbsAttribute>().Any())
            .Select(Name)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Actions(), Is.Not.Empty);
            Assert.That(unmarked, Is.Empty);
        }
    }

    [Test]
    public void EveryAction_SaysWhoMayCallIt()
    {
        // [Authorize] (with its roles) or [AllowAnonymous], on the action or its controller: none is open by accident
        var unmarked = Actions()
            .Where(a => a.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is null
                && a.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is null
                && a.DeclaringType!.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is null
                && a.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is null)
            .Select(Name)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Actions(), Is.Not.Empty);
            Assert.That(unmarked, Is.Empty);
        }
    }

    [Test]
    public void EveryAction_DeclaresItsSuccessForOpenApi()
    {
        // A ProducesResponseType for the 2xx (or the 302 of a sign-in's challenge) it answers, so the spec says what it returns
        var undeclared = Actions()
            .Where(a => !a.GetCustomAttributes<ProducesResponseTypeAttribute>().Any(p => p.StatusCode is >= 200 and < 400))
            .Select(Name)
            .ToList();

        Assert.That(undeclared, Is.Empty);
    }

    [Test]
    public void EveryAction_DeclaresWhatItsOkAnswersWith()
    {
        // A 200 says what its body is (a DTO, text, a file), so the client generated from the document reads it: without
        // a type the generated method expects no body and throws the answer away
        var untyped = Actions()
            .Where(a => a.GetCustomAttributes<ProducesResponseTypeAttribute>().Any(p => p.StatusCode == StatusCodes.Status200OK && p.Type == typeof(void)))
            .Select(Name)
            .ToList();

        Assert.That(untyped, Is.Empty);
    }

    /// <summary>What the server sets itself, from who is signed in or the route, whatever a request says.</summary>
    private static readonly string[] ServerSet = ["RequestedByUserID", "TriggeredByUserID", "Trigger"];

    [Test]
    public void EveryRequestBody_LeavesOutWhatTheServerSets()
    {
        // A body that names who asked (or what caused it) makes the generated client send a value the server throws away
        var offending = Actions()
            .SelectMany(a => a.GetParameters().Where(p => p.GetCustomAttribute<FromBodyAttribute>() is not null)
                .SelectMany(p => PropertyPaths(p.ParameterType, depth: 2))
                .Where(path => ServerSet.Contains(path.Split('.')[^1]))
                .Select(path => $"{Name(a)}: {path}"))
            .ToList();

        Assert.That(offending, Is.Empty);
    }

    [Test]
    public void TheSignupBody_LeavesOutWhoAndWhichSeason()
    {
        // The signed-in player's own signup, for the season in the route
        var body = typeof(GrifballWebApp.Server.Signups.SignupsController).GetMethod("UpsertSignup")!
            .GetParameters().Single(p => p.GetCustomAttribute<FromBodyAttribute>() is not null).ParameterType;

        Assert.That(body.GetProperties().Select(p => p.Name), Does.Not.Contain("UserID").And.Not.Contain("SeasonID"));
    }

    /// <summary>A type's public properties, and its app-type properties' own, as "Outer.Inner".</summary>
    private static IEnumerable<string> PropertyPaths(Type type, int depth)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).SelectMany(p =>
            depth > 1 && p.PropertyType.Namespace?.StartsWith("GrifballWebApp", StringComparison.Ordinal) == true && p.PropertyType.IsClass
                ? PropertyPaths(p.PropertyType, depth - 1).Select(inner => $"{p.Name}.{inner}").Prepend(p.Name)
                : [p.Name]);

    [Test]
    public void EveryAction_ReadsWithGet_AndChangesWithPost_AtItsOwnRoute()
    {
        // The owner's rules (#270): GET to read and POST for every change, no PUT/PATCH/DELETE; each action names its
        // resource route itself rather than [controller]/[action]
        var methods = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().SelectMany(h => h.HttpMethods).Select(m => (Action: Name(a), Method: m))).ToList();
        var templates = Actions().SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().Select(h => (Action: Name(a), h.Template))).ToList();
        var controllerRoutes = Actions().Select(a => a.DeclaringType!).Distinct().Where(t => t.GetCustomAttribute<RouteAttribute>() is not null).Select(t => t.Name).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(methods.Where(m => m.Method is not ("GET" or "POST")), Is.Empty);
            Assert.That(templates.Where(t => string.IsNullOrEmpty(t.Template) || t.Template.Contains('[')), Is.Empty);
            Assert.That(controllerRoutes, Is.Empty);
        }
    }

    [Test]
    public void EveryAction_HasANonEmptySummary()
    {
        // GenerateDocumentationFile (the .csproj) writes the Server assembly's XML doc file beside its DLL; a
        // project reference copies it here too, the same way IncludeXmlComments finds it in Program.cs.
        var assemblyPath = typeof(GrifballWebApp.Server.Seasons.SeasonController).Assembly.Location;
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
        Assert.That(File.Exists(xmlPath), Is.True, $"{xmlPath} is missing: is GenerateDocumentationFile set on GrifballWebApp.Server.csproj?");

        var members = XDocument.Load(xmlPath).Root!.Element("members")!.Elements("member")
            .ToDictionary(m => (string)m.Attribute("name")!, m => (string?)m.Element("summary"));

        var missing = Actions()
            .Where(a => !members.TryGetValue(DocId(a), out var summary) || string.IsNullOrWhiteSpace(summary))
            .Select(Name)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Actions(), Is.Not.Empty);
            Assert.That(missing, Is.Empty);
        }
    }

    [TestCase(StatusCodes.Status400BadRequest, "Request refused")]
    [TestCase(StatusCodes.Status401Unauthorized, "Not signed in")]
    [TestCase(StatusCodes.Status403Forbidden, "Forbidden")]
    [TestCase(StatusCodes.Status404NotFound, "Not found")]
    [TestCase(StatusCodes.Status409Conflict, "Conflict")]
    [TestCase(StatusCodes.Status502BadGateway, "Server error")]
    [TestCase(StatusCodes.Status418ImATeapot, "Request refused")]
    public void Refusal_IsAProblemDetails_WithTheStatusTitleAndDetail(int status, string title)
    {
        var controller = new ProbeController().WithAnonymousUser();

        var result = controller.Refusal(status, "Why");

        var problem = result.AssertProblem(status, "Why");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(problem.Title, Is.EqualTo(title));
            Assert.That(result.ContentTypes, Has.Exactly(1).EqualTo("application/problem+json"));
        }
    }

    [Test]
    public void Helpers_AnswerTheirStatus()
    {
        var controller = new ProbeController().WithAnonymousUser();

        using (Assert.EnterMultipleScope())
        {
            controller.BadRequestProblem("a").AssertProblem(StatusCodes.Status400BadRequest, "a");
            controller.UnauthorizedProblem("b").AssertProblem(StatusCodes.Status401Unauthorized, "b");
            controller.ForbiddenProblem("c").AssertProblem(StatusCodes.Status403Forbidden, "c");
            controller.NotFoundProblem("d").AssertProblem(StatusCodes.Status404NotFound, "d");
            controller.ConflictProblem("e").AssertProblem(StatusCodes.Status409Conflict, "e");
        }
    }

    private sealed class ProbeController : ControllerBase;
}

[TestFixture]
public class ApiLocationsTests
{
    private static DefaultHttpContext Answer(int status, string? location, string host = "grifball.example")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.Response.StatusCode = status;
        if (location is not null)
            context.Response.Headers.Location = location;
        return context;
    }

    [TestCase(StatusCodes.Status201Created, "https://grifball.example/seasons/12/settings", "https://grifball.example/api/seasons/12/settings")]
    [TestCase(StatusCodes.Status202Accepted, "/historical-imports/4", "/api/historical-imports/4")]
    [TestCase(StatusCodes.Status201Created, "https://grifball.example/api/seasons/12/settings", "https://grifball.example/api/seasons/12/settings")]
    [TestCase(StatusCodes.Status201Created, "/api/seasons/12/settings", "/api/seasons/12/settings")]
    [TestCase(StatusCodes.Status201Created, "https://elsewhere.example/x", "https://elsewhere.example/x")]
    [TestCase(StatusCodes.Status201Created, "//elsewhere.example/x", "//elsewhere.example/x")]
    [TestCase(StatusCodes.Status201Created, "relative/x", "relative/x")]
    [TestCase(StatusCodes.Status302Found, "/signin-discord", "/signin-discord")]
    public void Prefix_PutsACreatedOrAcceptedLocationOnThisHostUnderTheApi(int status, string location, string expected)
    {
        var context = Answer(status, location);

        ApiLocations.Prefix(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(expected));
    }

    [TestCase("grifball.example:443", "https://grifball.example:443/seasons/12/settings", "https://grifball.example/api/seasons/12/settings")]
    [TestCase("grifball.example", "https://grifball.example:443/seasons/12/settings", "https://grifball.example/api/seasons/12/settings")]
    [TestCase("grifball.example:443", "https://grifball.example/seasons/12/settings", "https://grifball.example/api/seasons/12/settings")]
    [TestCase("grifball.example:8443", "https://grifball.example/seasons/12/settings", "https://grifball.example/seasons/12/settings")]
    public void Prefix_TreatsADefaultPortAsTheSameHost(string host, string location, string expected)
    {
        var context = Answer(StatusCodes.Status201Created, location, host);

        ApiLocations.Prefix(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Prefix_KeepsAnEscapedPathEscaped_AndTheQuery()
    {
        var context = Answer(StatusCodes.Status201Created, "https://grifball.example/sources/a%20b?x=1");

        ApiLocations.Prefix(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("https://grifball.example/api/sources/a%20b?x=1"));
    }

    [Test]
    public void Prefix_LeavesAnAnswerWithoutALocation()
    {
        var context = Answer(StatusCodes.Status201Created, null);

        ApiLocations.Prefix(context);

        Assert.That(context.Response.Headers.ContainsKey("Location"), Is.False);
    }
}
