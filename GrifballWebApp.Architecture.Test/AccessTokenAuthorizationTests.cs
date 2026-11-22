using System.Reflection;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// Default-deny for personal access tokens (#549): every Sysadmin-only action (<see cref="Authorize"/> naming that role
/// alone, on the action or its controller), and every action on an Identity or external-login controller (self-service
/// account actions, which aren't Sysadmin-gated but are just as sensitive), must choose explicitly whether a token may
/// call it (<see cref="DenyAccessTokenAttribute"/> or <see cref="AllowAccessTokenAttribute"/>). A new sensitive action
/// that forgets fails this test instead of staying open by default.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AccessTokenAuthorizationTests
{
    /// <summary>Controllers whose self-service actions are just as sensitive as a Sysadmin-only one, though none of them name a role.</summary>
    private static readonly Type[] AccountControllers = [typeof(IdentityController), typeof(ExternalLoginsController)];

    private static IEnumerable<MethodInfo> Actions() =>
        typeof(GrifballWebApp.Server.Seasons.SeasonController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";

    /// <summary>The roles an action's own [Authorize] names (its method's, else its controller's); empty for none, or for [AllowAnonymous].</summary>
    private static string[] Roles(MethodInfo action)
    {
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>(inherit: true) ?? action.DeclaringType!.GetCustomAttribute<AuthorizeAttribute>(inherit: true);
        return authorize?.Roles?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
    }

    /// <summary>Sysadmin and no other role, or a controller this rule names regardless of its own roles.</summary>
    private static bool IsSensitive(MethodInfo action)
        => Roles(action) is ["Sysadmin"] || AccountControllers.Contains(action.DeclaringType);

    private static bool HasAnExplicitChoice(MethodInfo action)
        => action.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null
            || action.DeclaringType!.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.DeclaringType!.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null;

    [Test]
    public void EverySensitiveAction_ChoosesExplicitly_ForAccessTokens()
    {
        var sensitive = Actions().Where(IsSensitive).ToList();
        var undecided = sensitive.Where(a => !HasAnExplicitChoice(a)).Select(Name).ToList();

        using (Assert.EnterMultipleScope())
        {
            // Both rules actually match something today, so a change to the rules themselves can't silently stop testing anything.
            Assert.That(sensitive, Is.Not.Empty);
            Assert.That(sensitive.Any(a => Roles(a) is ["Sysadmin"]), Is.True, "the Sysadmin-only rule matches something");
            Assert.That(sensitive.Any(a => AccountControllers.Contains(a.DeclaringType)), Is.True, "the account-controller rule matches something");
            Assert.That(undecided, Is.Empty, "mark it [DenyAccessToken] or [AllowAccessToken]");
        }
    }
}
