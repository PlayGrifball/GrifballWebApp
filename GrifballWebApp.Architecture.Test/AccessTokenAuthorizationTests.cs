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

    /// <summary>An action as it's actually reached: the concrete controller the route maps to (whose own attributes
    /// apply, even when it didn't declare the method itself) and the method (whose own, and its declaring type's -
    /// a base controller's - apply too). <see cref="GetMethods(Type)"/> below is <see cref="BindingFlags.DeclaredOnly"/>-free
    /// on purpose: an action inherited from a base controller, never overridden, is still reachable and sensitive.</summary>
    private readonly record struct Action(Type Controller, MethodInfo Method);

    private static IEnumerable<Action> Actions() =>
        typeof(GrifballWebApp.Server.Seasons.SeasonController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => GetMethods(t).Select(m => new Action(t, m)));

    /// <summary>Every action method a controller answers, including one it inherits without overriding: every public
    /// instance method declared somewhere between it and (not including) ControllerBase/Controller themselves.</summary>
    private static IEnumerable<MethodInfo> GetMethods(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null
                && typeof(ControllerBase).IsAssignableFrom(m.DeclaringType) && m.DeclaringType != typeof(ControllerBase) && m.DeclaringType != typeof(Controller));

    private static string Name(Action action) => $"{action.Controller.Name}.{action.Method.Name}";

    /// <summary>
    /// Every <see cref="AuthorizeAttribute"/> that applies to the action: the method's own, its declaring type's (a
    /// base controller's, for an inherited action), and the concrete controller's, since all apply at runtime, stacked
    /// and ANDed (a request must satisfy every one). Each is kept separate, not merged into one role set: a class
    /// <c>Roles="Sysadmin"</c> plus a method <c>Roles="Commissioner"</c> requires both roles together (more
    /// restrictive than Sysadmin alone, not a weaker "either"), so asking "is any ONE of these, by itself,
    /// Sysadmin-only" is what finds that this action is at least that sensitive, not asking whether the union of
    /// every role mentioned anywhere happens to equal exactly one.
    /// </summary>
    private static IEnumerable<AuthorizeAttribute> Authorizes(Action action)
        => action.Method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.Method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Concat(action.Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    private static string[] RolesOf(AuthorizeAttribute authorize)
        => authorize.Roles?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static bool IsAllowAnonymous(Action action)
        => action.Method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || action.Method.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || action.Controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

    /// <summary>
    /// Sysadmin-only: not <see cref="AllowAnonymousAttribute"/> (which skips authorization outright, whatever
    /// <see cref="AuthorizeAttribute"/> says), and at least one applicable <c>[Authorize]</c>, by itself, names
    /// Sysadmin and no other role. Or a controller this rule names regardless of its own roles.
    /// </summary>
    private static bool IsSensitive(Action action)
        => (!IsAllowAnonymous(action) && Authorizes(action).Any(a => RolesOf(a) is ["Sysadmin"]))
            || AccountControllers.Contains(action.Controller);

    private static bool HasAnExplicitChoice(Action action)
        => action.Method.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.Method.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null
            || action.Method.DeclaringType!.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.Method.DeclaringType!.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null
            || action.Controller.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.Controller.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null;

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
            Assert.That(sensitive.Any(a => AccountControllers.Contains(a.Controller)), Is.True, "the account-controller rule matches something");
            Assert.That(undecided, Is.Empty, "mark it [DenyAccessToken] or [AllowAccessToken]");
        }
    }

    /// <summary>
    /// Every <see cref="GrifballWebApp.Server.Historical.HistoricalImportController"/> action that queues merge-capable
    /// work (<see cref="DenyAccessTokenAttribute"/>, since the merge guard inside <c>UserMergeService.Merge</c> can't
    /// catch it: the worker runs it with no request of its own) is denied; every other action on it stays allowed.
    /// </summary>
    [Test]
    public void HistoricalImportController_DeniesTheActionsThatQueueAMerge_AllowsTheRest()
    {
        var actions = Actions().Where(a => a.Controller == typeof(GrifballWebApp.Server.Historical.HistoricalImportController)).ToList();
        var denied = actions.Where(a => a.Method.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null).Select(a => a.Method.Name).ToList();
        string[] expectedDenied = ["Import", "Preview", "ApplyPreview", "ApproveReview"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actions, Is.Not.Empty);
            Assert.That(denied, Is.EquivalentTo(expectedDenied));
            Assert.That(actions.Select(a => a.Method.Name), Is.SupersetOf(expectedDenied), "the controller still has these actions");
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

        Assert.That(IsSensitive(new Action(typeof(RolesFixture), method)), Is.True);
    }

    /// <summary>[AllowAnonymous] skips authorization outright: a Sysadmin-named role left on the action or its
    /// controller by mistake means nothing at runtime, so this must not be flagged sensitive by the role rule.</summary>
    [Test]
    public void IsSensitive_AllowAnonymousWithSysadminRole_IsNotSensitiveByTheRoleRule()
    {
        var method = typeof(RolesFixture).GetMethod(nameof(RolesFixture.AnonymousDespiteSysadmin), FixtureMethod)!;

        Assert.That(IsSensitive(new Action(typeof(RolesFixture), method)), Is.False);
    }

    /// <summary>
    /// An action inherited from a base fixture, never overridden by the concrete one: still sensitive, because
    /// <see cref="Authorizes"/> reads the method's declaring type (the base), not only the concrete controller - the
    /// gap <see cref="GetMethods"/> and this closed (previously <see cref="BindingFlags.DeclaredOnly"/> missed it
    /// entirely).
    /// </summary>
    [Test]
    public void IsSensitive_AnInheritedAction_UsesItsDeclaringTypesAttributes()
    {
        var method = typeof(DerivedFixture).GetMethod(nameof(BaseFixture.InheritedSysadminAction), BindingFlags.Public | BindingFlags.Instance)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(method.DeclaringType, Is.EqualTo(typeof(BaseFixture)), "inherited, not overridden");
            Assert.That(IsSensitive(new Action(typeof(DerivedFixture), method)), Is.True);
        }
    }

    /// <summary>
    /// <see cref="GetMethods"/> finds an inherited action by walking up from the concrete type, the way reflection's
    /// own non-<see cref="BindingFlags.DeclaredOnly"/> search does.
    /// </summary>
    [Test]
    public void GetMethods_FindsAnInheritedAction_NotJustTheConcreteTypesOwn()
        => Assert.That(GetMethods(typeof(DerivedFixture)).Select(m => m.Name), Does.Contain(nameof(BaseFixture.InheritedSysadminAction)));

    /// <summary>A reflection-only fixture (never instantiated): its methods are static since nothing in this test touches an instance.</summary>
    [Authorize(Roles = "Sysadmin")]
    private sealed class RolesFixture
    {
        [Authorize(Roles = "Commissioner")]
        public static void SysadminAndCommissioner() { }

        [AllowAnonymous]
        public static void AnonymousDespiteSysadmin() { }
    }

    /// <summary>
    /// Stands in for a base controller: declares an action a derived one inherits without overriding. An instance
    /// method, like a real action must be (<see cref="GetMethods"/> only looks for those); touches
    /// <see cref="ControllerBase.ControllerContext"/> so it isn't flagged as one that could be static, though it is
    /// never actually called - only reflected on.
    /// </summary>
    private class BaseFixture : ControllerBase
    {
        [Authorize(Roles = "Sysadmin")]
        public void InheritedSysadminAction() => _ = ControllerContext;
    }

    /// <summary>Stands in for a concrete controller that never overrides the base's action.</summary>
    private sealed class DerivedFixture : BaseFixture;
}
