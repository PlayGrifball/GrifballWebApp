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
    /// The roles an action requires: its method's own <see cref="AuthorizeAttribute"/>(s) combined with its controller's,
    /// since both apply at runtime (a bare method-level <c>[Authorize]</c> with no roles does not hide the controller's
    /// <c>Roles="Sysadmin"</c>). <see cref="MemberInfo.GetCustomAttributes{T}"/>, plural, so an action with more than one
    /// <c>[Authorize]</c> (legal: <see cref="AttributeUsageAttribute.AllowMultiple"/> is true for it) does not throw the
    /// way the singular <see cref="MemberInfo.GetCustomAttribute{T}"/> would. Empty for none, or for [AllowAnonymous].
    /// </summary>
    private static string[] Roles(MethodInfo action)
        => action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .SelectMany(a => a.Roles?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Distinct()
            .ToArray();

    /// <summary>Sysadmin and no other role, or a controller this rule names regardless of its own roles.</summary>
    private static bool IsSensitive(MethodInfo action)
        => Roles(action) is ["Sysadmin"] || AccountControllers.Contains(action.DeclaringType);

    private static bool HasAnExplicitChoice(MethodInfo action)
        => action.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null
            || action.DeclaringType!.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.DeclaringType!.GetCustomAttribute<AllowAccessTokenAttribute>(inherit: true) is not null;

    private static bool IsDenied(MethodInfo action)
        => action.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null
            || action.DeclaringType!.GetCustomAttribute<DenyAccessTokenAttribute>(inherit: true) is not null;

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

    /// <summary>
    /// Every action that merges one user into another, or undoes such a merge (<see cref="MergesUsersAttribute"/>),
    /// wherever it lives, must be <see cref="DenyAccessTokenAttribute"/>: a leaked sysadmin token must never be able to
    /// merge, or merge and then be unable to undo it (a controller's own <c>[AllowAccessToken]</c> must not cover one of
    /// these just because most of that controller's other actions are fine for a token).
    /// </summary>
    [Test]
    public void EveryMergeAction_DeniesAccessTokens()
    {
        var merges = Actions().Where(a => a.GetCustomAttribute<MergesUsersAttribute>(inherit: true) is not null).ToList();
        var allowed = merges.Where(a => !IsDenied(a)).Select(Name).ToList();

        using (Assert.EnterMultipleScope())
        {
            // The rule matches something today, so a change to it can't silently stop testing anything.
            Assert.That(merges, Is.Not.Empty);
            Assert.That(allowed, Is.Empty, "a merge or undo-merge action must be [DenyAccessToken]");
        }
    }

    private static readonly string[] JustSysadmin = ["Sysadmin"];

    private const BindingFlags FixtureMethod = BindingFlags.Public | BindingFlags.Static;

    /// <summary>A method-level bare [Authorize] must not hide a controller's Roles="Sysadmin": both apply at runtime.</summary>
    [Test]
    public void Roles_CombinesClassAndMethodAuthorizeAttributes()
    {
        var method = typeof(RolesFixture).GetMethod(nameof(RolesFixture.BareAuthorize), FixtureMethod)!;

        var roles = Roles(method);

        Assert.That(roles, Is.EqualTo(JustSysadmin));
    }

    /// <summary>More than one [Authorize] on the same action or controller (legal) must not throw.</summary>
    [Test]
    public void Roles_NeverThrowsOnMultipleAuthorizeAttributes()
    {
        var method = typeof(RolesFixture).GetMethod(nameof(RolesFixture.TwoAuthorizeAttributes), FixtureMethod)!;

        Assert.That(() => Roles(method), Throws.Nothing);
    }

    /// <summary>A reflection-only fixture (never instantiated): its methods are static since nothing in this test touches an instance.</summary>
    [Authorize(Roles = "Sysadmin")]
    private sealed class RolesFixture
    {
        [Authorize]
        public static void BareAuthorize() { }

        [Authorize(Roles = "Commissioner")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public static void TwoAuthorizeAttributes() { }
    }
}
