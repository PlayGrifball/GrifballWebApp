using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Test.CovD;

internal static class ControllerTestHelpers_d
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

internal sealed class FixedTimeProvider_d(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
