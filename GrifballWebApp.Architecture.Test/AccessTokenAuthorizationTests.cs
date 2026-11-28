using System.Reflection;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// Default-deny for personal access tokens (#549): every staff-only action (<see cref="Authorize"/> naming one or more
/// roles, none of which is <c>Player</c> - <c>Roles = "Sysadmin"</c> and <c>Roles = "Commissioner,Sysadmin"</c> both
/// qualify, and so does plain <c>Roles = "Commissioner"</c>; #561 marked <c>StaffStatFlagsController</c> by hand before
/// this test could see its kind, #566 widened the test from "Sysadmin alone" to this), and every action on an Identity
/// or external-login controller (self-service account actions, which aren't Sysadmin-gated but are just as sensitive),
/// must choose explicitly whether a token may call it (<see cref="DenyAccessTokenAttribute"/> or
/// <see cref="AllowAccessTokenAttribute"/>). A new sensitive action that forgets fails this test instead of staying
/// open by default.
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

    /// <summary>Any role named at all, none of them <c>Player</c>: staff-only, whether it's Sysadmin alone, a plain
    /// <c>Commissioner</c>, or several roles together such as <c>Commissioner,Sysadmin</c> (an OR, not an AND -
    /// still staff-only, since every option named is staff). Narrowing a Sysadmin-only action to plain Commissioner
    /// (or the reverse) must not drop out of this rule just because the set changed shape.</summary>
    private static bool IsStaffOnly(string[] roles) => roles.Length > 0 && !roles.Contains("Player");

    private static bool IsAllowAnonymous(Action action)
        => action.Method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || action.Method.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || action.Controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

    /// <summary>
    /// Staff-only (<see cref="IsStaffOnly"/>): not <see cref="AllowAnonymousAttribute"/> (which skips authorization
    /// outright, whatever <see cref="AuthorizeAttribute"/> says), and at least one applicable <c>[Authorize]</c>, by
    /// itself, names one or more roles with no Player among them. Or a controller this rule names regardless of its
    /// own roles.
    /// </summary>
    private static bool IsSensitive(Action action)
        => (!IsAllowAnonymous(action) && Authorizes(action).Any(a => IsStaffOnly(RolesOf(a))))
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
            Assert.That(sensitive.Any(a => Authorizes(a).Any(x => RolesOf(x) is ["Sysadmin"])), Is.True, "the staff-only rule matches a single-role (Sysadmin) action");
            Assert.That(sensitive.Any(a => Authorizes(a).Any(x => RolesOf(x).Length > 1 && IsStaffOnly(RolesOf(x)))), Is.True, "the staff-only rule matches a several-roles action");
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

    /// <summary>Several roles, none of them Player (as <c>Commissioner,Sysadmin</c> is): staff-only, even though no
    /// single applicable [Authorize] names Sysadmin alone.</summary>
    [Test]
    public void IsSensitive_SeveralStaffRolesNoneOfThemPlayer_IsSensitive()
    {
        var method = typeof(SeveralRolesFixture).GetMethod(nameof(SeveralRolesFixture.CommissionerAndSysadmin), FixtureMethod)!;

        Assert.That(IsSensitive(new Action(typeof(SeveralRolesFixture), method)), Is.True);
    }

    /// <summary>A single non-Sysadmin role (plain Commissioner): staff-only too, so narrowing a Sysadmin-only action
    /// to Commissioner can't drop it out of the rule (#566: the gap the review found - Availability, Brackets,
    /// MatchPlanner, SeasonMatch and Teams all had plain-Commissioner actions with no access-token choice).</summary>
    [Test]
    public void IsSensitive_ASingleNonPlayerRole_IsSensitive()
    {
        var method = typeof(SeveralRolesFixture).GetMethod(nameof(SeveralRolesFixture.CommissionerAlone), FixtureMethod)!;

        Assert.That(IsSensitive(new Action(typeof(SeveralRolesFixture), method)), Is.True);
    }

    private static readonly string[] CommissionerAndPlayer = ["Commissioner", "Player"];
    private static readonly string[] CommissionerAlone = ["Commissioner"];
    private static readonly string[] NoRoles = [];

    /// <summary>Several roles including Player: open to ordinary players too, so not staff-only (the action may
    /// still be sensitive some other way, just not this one).</summary>
    [Test]
    public void IsStaffOnly_RolesIncludingPlayer_IsFalse()
        => Assert.That(IsStaffOnly(CommissionerAndPlayer), Is.False);

    /// <summary>A single non-Player role is staff-only: the general rule (#566) catches what the old
    /// several-roles-only one missed.</summary>
    [Test]
    public void IsStaffOnly_ASingleNonPlayerRole_IsTrue()
        => Assert.That(IsStaffOnly(CommissionerAlone), Is.True);

    /// <summary>No role named at all isn't staff-only by this rule (an unauthenticated or any-signed-in-user action):
    /// the account-controller and Identity rules catch sensitivity that names no role, not this one.</summary>
    [Test]
    public void IsStaffOnly_NoRoles_IsFalse()
        => Assert.That(IsStaffOnly(NoRoles), Is.False);

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

    /// <summary>No class-level role, unlike <see cref="RolesFixture"/>: isolates each case (several staff roles, a
    /// single non-Sysadmin role) from the Sysadmin-alone one.</summary>
    private sealed class SeveralRolesFixture
    {
        [Authorize(Roles = "Commissioner,Sysadmin")]
        public static void CommissionerAndSysadmin() { }

        [Authorize(Roles = "Commissioner")]
        public static void CommissionerAlone() { }
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
