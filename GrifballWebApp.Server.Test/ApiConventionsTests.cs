using System.Reflection;
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
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";

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
