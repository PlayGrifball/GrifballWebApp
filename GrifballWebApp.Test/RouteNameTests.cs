using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GrifballWebApp.Test;

/// <summary>
/// ASP.NET refuses to start when two actions share a route name with different templates (a preview crash-looped on
/// two "GetSeasons"): every named route of the server's controllers is unique.
/// </summary>
[TestFixture]
public class RouteNameTests
{
    [Test]
    public void EveryRouteName_IsUnique()
    {
        var names = typeof(GrifballWebApp.Server.Seasons.SeasonController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => (a.Name, Action: $"{m.DeclaringType!.Name}.{m.Name}")))
            .Where(r => r.Name is not null)
            .ToList();

        var clashes = names.GroupBy(r => r.Name).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(r => r.Action))}").ToList();

        Assert.That(names, Is.Not.Empty);
        Assert.That(clashes, Is.Empty);
    }
}
