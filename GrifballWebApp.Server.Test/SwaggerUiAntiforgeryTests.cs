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

        // A variable, not the constant: NUnit wants the actual value to be one
        string interceptor = SwaggerUiAntiforgery.RequestInterceptor;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(interceptor, Does.Contain($"'/{route}'"));
            Assert.That(interceptor, Does.Contain($"request.headers['{BrowserAuth.AntiforgeryHeader}']"));
        }
    }
}
