using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Test;

internal static class ControllerTestHelpers
{
    /// <summary>Attach an HttpContext whose User has the given NameIdentifier/name/roles. userId null = no NameIdentifier claim.</summary>
    internal static T WithUser<T>(this T controller, string? userId, string? name = null, params string[] roles) where T : ControllerBase
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (name is not null)
            claims.Add(new Claim(ClaimTypes.Name, name));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var identity = new ClaimsIdentity(claims, claims.Count > 0 ? "Test" : null);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
    }

    internal static T WithAnonymousUser<T>(this T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
        };
        return controller;
    }

    internal static AuthorizeAttribute? ClassAuthorize(Type controller) =>
        controller.GetCustomAttribute<AuthorizeAttribute>(inherit: true);

    internal static MethodInfo Action(Type controller, string name) =>
        controller.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        ?? throw new InvalidOperationException($"No action {name} on {controller.Name}");

    internal static AuthorizeAttribute? ActionAuthorize(Type controller, string name) =>
        Action(controller, name).GetCustomAttribute<AuthorizeAttribute>(inherit: true);

    internal static bool ActionAllowsAnonymous(Type controller, string name) =>
        Action(controller, name).GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
}

internal static class ProblemAssertions
{
    /// <summary>The result is a ProblemDetails refusal with this status (and detail, when given); returns it.</summary>
    internal static ProblemDetails AssertProblem(this IActionResult? result, int status, string? detail = null)
    {
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result!;
        Assert.That(objectResult.Value, Is.InstanceOf<ProblemDetails>(), "the body is a ProblemDetails, not a bare string");
        var problem = (ProblemDetails)objectResult.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(objectResult.StatusCode ?? problem.Status, Is.EqualTo(status));
            Assert.That(problem.Status, Is.EqualTo(status));
            if (detail is not null)
                Assert.That(problem.Detail, Is.EqualTo(detail));
        }
        return problem;
    }

    /// <summary>The same for an ActionResult&lt;T&gt;'s result.</summary>
    internal static ProblemDetails AssertProblem<T>(this ActionResult<T> result, int status, string? detail = null)
        => result.Result.AssertProblem(status, detail);
}
