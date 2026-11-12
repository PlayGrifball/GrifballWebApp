using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace GrifballWebApp.Server.Test;

/// <summary>Swagger UI's request interceptor asks the real antiforgery route and sends its token in the real header.</summary>
[TestFixture]
public class SwaggerUiAntiforgeryTests
{
    [Test]
    public void TheInterceptor_UsesTheAntiforgeryRoute_AndHeader()
    {
        var route = typeof(IdentityController).GetMethod(nameof(IdentityController.Antiforgery))!.GetCustomAttribute<HttpGetAttribute>()!.Template;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SwaggerUiAntiforgery.RequestInterceptor, Does.Contain($"'/{route}'"));
            Assert.That(SwaggerUiAntiforgery.RequestInterceptor, Does.Contain($"request.headers['{BrowserAuth.AntiforgeryHeader}']"));
        }
    }
}
