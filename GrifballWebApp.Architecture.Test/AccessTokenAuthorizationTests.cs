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

    /// <summary>
    /// Every <see cref="AuthorizeAttribute"/> that applies to the action: its method's own and its controller's, since
    /// both apply at runtime, stacked and ANDed (a request must satisfy every one). Each is kept separate, not merged
    /// into one role set: a class <c>Roles="Sysadmin"</c> plus a method <c>Roles="Commissioner"</c> requires both roles
    /// together (more restrictive than Sysadmin alone, not a weaker "either"), so asking "is any ONE of these,
    /// by itself, Sysadmin-only" is what finds that this action is at least that sensitive, not asking whether the
    /// union of every role mentioned anywhere happens to equal exactly one.
    /// </summary>
    private static IEnumerable<AuthorizeAttribute> Authorizes(MethodInfo action)
        => action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    private static string[] RolesOf(AuthorizeAttribute authorize)
        => authorize.Roles?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static bool IsAllowAnonymous(MethodInfo action)
        => action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || action.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

    /// <summary>
    /// Sysadmin-only: not <see cref="AllowAnonymousAttribute"/> (which skips authorization outright, whatever
    /// <see cref="AuthorizeAttribute"/> says), and at least one applicable <c>[Authorize]</c>, by itself, names
    /// Sysadmin and no other role. Or a controller this rule names regardless of its own roles.
    /// </summary>
    private static bool IsSensitive(MethodInfo action)
        => (!IsAllowAnonymous(action) && Authorizes(action).Any(a => RolesOf(a) is ["Sysadmin"]))
            || AccountControllers.Contains(action.DeclaringType);

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
            Assert.That(sensitive.Any(a => Authorizes(a).Any(x => RolesOf(x) is ["Sysadmin"])), Is.True, "the Sysadmin-only rule matches something");
            Assert.That(sensitive.Any(a => AccountControllers.Contains(a.DeclaringType)), Is.True, "the account-controller rule matches something");
            Assert.That(undecided, Is.Empty, "mark it [DenyAccessToken] or [AllowAccessToken]");
        }
    }

    private const BindingFlags FixtureMethod = BindingFlags.Public | BindingFlags.Static;

    /// <summary>
    /// Stacked [Authorize] are ANDed, not unioned: a class Sysadmin + a method Commissioner together require both
    /// roles, which is at least as sensitive as Sysadmin alone, and must still be caught (by the class attribute,
    /// looked at by itself) rather than escape because the combined set isn't exactly ["Sysadmin"].
    /// </summary>
    [Test]
    public void IsSensitive_ClassSysadminAndMethodCommissioner_IsStillSensitive()
    {
        var method = typeof(RolesFixture).GetMethod(nameof(RolesFixture.SysadminAndCommissioner), FixtureMethod)!;

        Assert.That(IsSensitive(method), Is.True);
    }

    /// <summary>[AllowAnonymous] skips authorization outright: a Sysadmin-named role left on the action or its
    /// controller by mistake means nothing at runtime, so this must not be flagged sensitive by the role rule.</summary>
    [Test]
    public void IsSensitive_AllowAnonymousWithSysadminRole_IsNotSensitiveByTheRoleRule()
    {
        var method = typeof(RolesFixture).GetMethod(nameof(RolesFixture.AnonymousDespiteSysadmin), FixtureMethod)!;

        Assert.That(IsSensitive(method), Is.False);
    }

    /// <summary>A reflection-only fixture (never instantiated): its methods are static since nothing in this test touches an instance.</summary>
    [Authorize(Roles = "Sysadmin")]
    private sealed class RolesFixture
    {
        [Authorize(Roles = "Commissioner")]
        public static void SysadminAndCommissioner() { }

        [AllowAnonymous]
        public static void AnonymousDespiteSysadmin() { }
    }
}
