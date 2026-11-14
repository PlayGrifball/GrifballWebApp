using System.ComponentModel;
using System.Reflection;
using System.Text;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Who a tool runs as: a site user with their roles (an MCP client's token holder, the Discord user's linked account), or
/// <see cref="Anonymous"/> (a Discord user with no linked account), who can use every tool that isn't about "you".
/// </summary>
public sealed record ToolCaller(int? UserID, IReadOnlyList<string> Roles)
{
    public static readonly ToolCaller Anonymous = new(null, []);

    /// <summary>
    /// Whether others see the answers: a Discord channel's, where the bot's chat replies. Then the caller sees seasons as the public
    /// does, whatever their roles: a season not announced yet stays out of a reply its players read (#435).
    /// </summary>
    public bool InPublic { get; init; }

    /// <summary>Which seasons the caller sees: staff the ones not announced yet too, unless <see cref="InPublic"/>.</summary>
    public SeasonViewer Viewer => InPublic ? SeasonViewer.Public : SeasonVisibility.For(Roles);

    /// <summary>Whether the caller has one of <paramref name="roles"/>; with none given, anyone does.</summary>
    public bool IsInAnyRole(IReadOnlyCollection<string> roles) => SiteRoles.IsInAnyRole(Roles, roles);

    /// <summary>Nothing, when the caller has one of <paramref name="roles"/>; a <see cref="ToolException"/> saying who may, else.</summary>
    public void Require(IReadOnlyCollection<string> roles)
    {
        if (!IsInAnyRole(roles))
            throw new ToolException(SiteRoles.MissingRole(roles));
    }

    /// <summary>
    /// Nothing, when the caller has one of the roles the tool <paramref name="tool"/> (a tool class's method, by its own name:
    /// left out, the calling one) names in its [ToolRoles]; a <see cref="ToolException"/> saying who may, else. A host's
    /// filtering isn't the only guard.
    /// </summary>
    public void RequireRolesOf([System.Runtime.CompilerServices.CallerMemberName] string tool = "") => Require(GrifTools.RolesByName[GrifTools.Name(tool)]);

    /// <summary>The caller's user ID; a <see cref="ToolException"/> for an anonymous caller, who must name the player.</summary>
    public int Me() => UserID ?? throw new ToolException(
        "You have no site account linked here, so name the player (search finds them by gamertag). On Discord, link your account on the site's profile page.");
}

/// <summary>
/// A refusal the model should see and can act on (no such season, say). The host passes its message on: the MCP server as an
/// error result, the chat hosts as the tool's result (<see cref="GrifTools.AIFunctions"/>). Any other exception's message
/// stays on the server.
/// </summary>
public class ToolException(string message) : Exception(message);

/// <summary>What a set of tools does to the platform, for the hosts to tell clients (MCP's tool annotations).</summary>
public enum ToolEffect
{
    /// <summary>Reads what the site shows, and changes nothing.</summary>
    ReadsOnly,
    /// <summary>Queues a pull from Halo, a background job that saves the games it finds: adds data, never changes or deletes any.</summary>
    QueuesHaloPull,
    /// <summary>
    /// Changes the league's data as staff's pages do (a result, a schedule, a bracket, a claim, a job): commissioners' and sysadmins'
    /// actions, which an MCP client asks the user about before it calls them.
    /// </summary>
    ChangesData,
}

/// <summary>
/// The site roles that may use a tool (on its method, or its class for all of them), as its endpoint's
/// <c>[Authorize(Roles = ...)]</c> names them: any one will do. A host gives a caller without one none of these tools
/// (<see cref="GrifTools.Allows"/>), and the tool refuses them too (<see cref="ToolCaller.Require"/>). None: anyone may.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ToolRolesAttribute(params string[] roles) : Attribute
{
    public IReadOnlyList<string> Roles { get; } = roles;
}

/// <summary>
/// The platform's tools for AI agents: one set, for every host (the MCP server, the Discord bot, the site's chat). A tool is a
/// public method with a [Description] on one of these classes, each parameter described too. Each calls the same services as
/// the controllers, and shows the caller no more than the site would. A host makes a new instance for each call from its
/// request's services (<see cref="Factory"/>).
/// </summary>
public static class GrifTools
{
    /// <summary>The classes of the tools that only read: open to anyone on the site, but staff's (<see cref="StaffTools"/>), each for its roles.</summary>
    public static readonly Type[] ReadOnly =
        [typeof(SeasonTools), typeof(TeamTools), typeof(PlayerTools), typeof(MatchTools), typeof(BrowseTools), typeof(ClockTools), typeof(HaloTools), typeof(StaffTools)];

    /// <summary>The classes of the tools that queue a pull from Halo (<see cref="ToolEffect.QueuesHaloPull"/>).</summary>
    public static readonly Type[] QueuingHaloPulls = [typeof(HaloPullTools)];

    /// <summary>The classes of staff's tools that change the league's data (<see cref="ToolEffect.ChangesData"/>), each tool for its roles.</summary>
    public static readonly Type[] ChangingData = [typeof(CommissionerTools), typeof(SysadminTools)];

    /// <summary>Every tool class, with what its tools do.</summary>
    public static IEnumerable<(Type Type, ToolEffect Effect)> All
        => ReadOnly.Select(t => (t, ToolEffect.ReadsOnly))
            .Concat(QueuingHaloPulls.Select(t => (t, ToolEffect.QueuesHaloPull)))
            .Concat(ChangingData.Select(t => (t, ToolEffect.ChangesData)));

    /// <summary>
    /// The tools a chat with a small model gets (the Discord bot, the site's chat): the questions people ask most. A model picks
    /// worse from a long list, so the rest are left to the MCP server's clients. Staff's tools aren't among them: the Discord bot
    /// answers in a public channel, which mustn't read a dashboard or a claim.
    /// </summary>
    public static readonly string[] Chat =
    [
        "who_am_i", "get_current_time", "search", "get_season", "get_season_standings", "get_season_schedule", "get_team",
        "get_player", "get_player_rank", "get_season_match", "get_recent_games", "get_leaderboards", "pull_season_match_games", "pull_xbox_account_games",
    ];

    /// <summary>The roles that may use a tool: its method's [ToolRoles], else its class's; none, anyone may.</summary>
    public static IReadOnlyList<string> Roles(MethodInfo method)
        => (method.GetCustomAttribute<ToolRolesAttribute>() ?? method.DeclaringType!.GetCustomAttribute<ToolRolesAttribute>())?.Roles ?? [];

    /// <summary>Whether a host gives <paramref name="caller"/> this tool: they have one of its roles, or it needs none.</summary>
    public static bool Allows(MethodInfo method, ToolCaller caller) => caller.IsInAnyRole(RolesByName[Name(method)]);

    /// <summary>Every tool's roles by its name, for a host that knows only the name (the MCP server's filters).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolesByName
        = All.SelectMany(set => Methods(set.Type)).ToDictionary(Name, Roles);

    /// <summary>A class's tools: its own public instance methods with a [Description].</summary>
    public static IEnumerable<MethodInfo> Methods(Type type)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<DescriptionAttribute>() is not null);

    /// <summary>A tool's name: its method's, in snake_case (GetSeasonStandings: get_season_standings).</summary>
    public static string Name(MethodInfo method) => Name(method.Name);

    /// <summary>The tool name of a method named <paramref name="methodName"/>: in snake_case.</summary>
    public static string Name(string methodName)
    {
        var name = new StringBuilder();
        foreach (var c in methodName)
        {
            if (char.IsUpper(c) && name.Length > 0)
                name.Append('_');
            name.Append(char.ToLowerInvariant(c));
        }
        return name.ToString();
    }

    /// <summary>
    /// How a host makes a tool class's instance for one call: from that call's services, given the caller when its constructor
    /// takes one. Worked out once per class.
    /// </summary>
    public static Func<IServiceProvider, ToolCaller, object> Factory(Type type)
    {
        var takesCaller = type.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(ToolCaller));
        var factory = ActivatorUtilities.CreateFactory(type, takesCaller ? [typeof(ToolCaller)] : []);
        return takesCaller ? (services, caller) => factory(services, [caller]) : (services, _) => factory(services, null);
    }

    /// <summary>
    /// The tools named in <paramref name="names"/> that <paramref name="caller"/> may use (<see cref="Allows"/>: a tool their
    /// roles don't allow isn't given at all) as functions a chat model calls (Microsoft.Extensions.AI), run as them. Each call runs in a scope of its own from <paramref name="scopes"/>, so a tool never shares a
    /// DbContext with the host (which may be saving meanwhile, as the Discord bot saves its streamed reply). A refusal comes back
    /// as the tool's result, so the model can tell the user why; the chat client's function invocation hides other errors' details.
    /// </summary>
    public static List<AITool> AIFunctions(IServiceScopeFactory scopes, ToolCaller caller, IReadOnlyCollection<string> names)
        => All.SelectMany(set =>
            {
                var create = Factory(set.Type);
                return Methods(set.Type).Select(method => (Method: method, Create: create));
            })
            .Where(t => names.Contains(Name(t.Method)) && Allows(t.Method, caller))
            .Select(t => (AITool)new InAScopeOfItsOwn(AIFunctionFactory.Create(t.Method, args => t.Create(args.Services!, caller),
                new AIFunctionFactoryOptions { Name = Name(t.Method) }), scopes))
            .ToList();

    /// <summary>A function run in a new scope per call, whose <see cref="ToolException"/> is its result, told as a refusal.</summary>
    private sealed class InAScopeOfItsOwn(AIFunction inner, IServiceScopeFactory scopes) : DelegatingAIFunction(inner)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            arguments.Services = scope.ServiceProvider;
            try
            {
                return await base.InvokeCoreAsync(arguments, cancellationToken);
            }
            catch (ToolException e)
            {
                return $"Refused: {e.Message}";
            }
        }
    }
}
