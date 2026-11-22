using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using Microsoft.Extensions.Logging.Abstractions;
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

    /// <summary>The caller's user name, as the site records who did something (a schedule applied, a job paused); null when not known.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether others see the answers: a Discord channel's, where the bot's chat replies. Then the caller sees seasons as the public
    /// does, whatever their roles: a season not announced yet stays out of a reply its players read (#435).
    /// </summary>
    public bool InPublic { get; init; }

    /// <summary>
    /// Whether the caller counts as signed in, for a chat model that is only for one (<see cref="GrifballWebApp.Application.Chat.ChatModelOption.SignedIn"/>):
    /// the site's own user (<see cref="UserID"/> alone isn't enough: a Discord caller's is set for any linked account, even
    /// a placeholder's), or a Discord account linked to a real (non-placeholder) one, gamertag or not. False for
    /// <see cref="Anonymous"/>, and the default for anyone else until set.
    /// </summary>
    public bool SignedIn { get; init; }

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
/// A tool that calls Halo's API itself (a game pulled, the medals downloaded), in a set whose effect doesn't say so: hosts
/// tell clients it reaches outside the site, as they do for a set that queues Halo pulls.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ToolReachesHaloAttribute : Attribute;

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
    /// The tools a chat with a small model gets in public (the Discord bot in a channel): the questions people ask most. A model picks
    /// worse from a long list, so the rest are left to the MCP server's clients. Staff's tools aren't among them: the Discord bot
    /// answers in a public channel, which mustn't read a dashboard or a claim.
    /// </summary>
    public static readonly string[] Chat =
    [
        "who_am_i", "get_current_time", "search", "list_seasons", "get_season", "get_season_standings", "get_season_schedule", "get_team",
        "get_player", "get_player_rank", "get_season_match", "get_recent_games", "get_leaderboards", "pull_season_match_games", "pull_xbox_account_games",
        "suggest_draft_picks",
    ];

    /// <summary>
    /// The classes of staff's tools, each tool for its roles: the ones that read a dashboard, a claim or a job, and every class of the ones
    /// that change data (<see cref="ChangingData"/>), so a new one is never left out of <see cref="PrivateChat"/>.
    /// </summary>
    public static readonly Type[] Staff = [typeof(StaffTools), .. ChangingData];

    /// <summary>
    /// The tools a chat gets in private (the site's, a Discord DM): <see cref="Chat"/>'s, and staff's (<see cref="Staff"/>). Its replies are
    /// the caller's alone, so staff get their own tools there, as the MCP server gives them, each for its roles: a player gets none of them.
    /// </summary>
    public static readonly string[] PrivateChat = [.. Chat, .. Staff.SelectMany(Methods).Select(Name)];

    /// <summary>
    /// Every tool, as the MCP server gives them (<see cref="All"/>), each for its roles: for a chat in private with a model that picks well from
    /// a long list (the chat model option EveryTool), so it can answer what the short list can't (every season, a Late League season).
    /// </summary>
    public static readonly string[] Every = [.. All.SelectMany(set => Methods(set.Type)).Select(Name)];

    /// <summary>The roles that may use a tool: its method's [ToolRoles], else its class's; none, anyone may.</summary>
    public static IReadOnlyList<string> Roles(MethodInfo method)
        => (method.GetCustomAttribute<ToolRolesAttribute>() ?? method.DeclaringType!.GetCustomAttribute<ToolRolesAttribute>())?.Roles ?? [];

    /// <summary>Whether a host gives <paramref name="caller"/> this tool: they have one of its roles, or it needs none.</summary>
    public static bool Allows(MethodInfo method, ToolCaller caller) => caller.IsInAnyRole(RolesByName[Name(method)]);

    /// <summary>Every tool's roles by its name, for a host that knows only the name (the MCP server's filters).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolesByName
        = All.SelectMany(set => Methods(set.Type)).ToDictionary(Name, Roles);

    /// <summary>Every tool's effect by its name, for a host that knows only the name (a read-only access token's filter, which leaves out <see cref="ToolEffect.ChangesData"/>).</summary>
    public static readonly IReadOnlyDictionary<string, ToolEffect> EffectByName
        = All.SelectMany(set => Methods(set.Type).Select(m => (Name: Name(m), set.Effect))).ToDictionary(t => t.Name, t => t.Effect);

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
    /// Each call is logged to <paramref name="log"/>; a failure's message, which may quote what the model passed, reaches the function
    /// invocation's log and span only with <paramref name="sensitiveData"/> (Chat:Telemetry:SensitiveData).
    /// </summary>
    public static List<AITool> AIFunctions(IServiceScopeFactory scopes, ToolCaller caller, IReadOnlyCollection<string> names, ILogger? log = null,
        bool sensitiveData = false, bool approveChanges = true)
        => Functions
            .Where(t => names.Contains(t.Function.Name) && Allows(t.Method, caller))
            .Select(t => WaitingForApproval(new InAScopeOfItsOwn(t.Function, scopes, caller, log ?? NullLogger.Instance, sensitiveData),
                approveChanges && t.Effect == ToolEffect.ChangesData))
            .ToList();

    /// <summary>
    /// A tool that changes the league's data (<see cref="ToolEffect.ChangesData"/>) waits for the person's approval, unless they chose to
    /// approve changes automatically (<paramref name="waits"/> false): the chat's function invocation hands the call back instead of
    /// running it (GrifChat's ConfirmAsync). Reads and Halo pulls run when the model asks.
    /// </summary>
    private static AITool WaitingForApproval(AIFunction function, bool waits) => waits ? new ApprovalRequiredAIFunction(function) : function;

    /// <summary>Whether <paramref name="caller"/>'s roles allow any tool that changes data: a change to approve, so a choice of how.</summary>
    public static bool MayChangeData(ToolCaller caller) => ChangingDataTools.Any(m => Allows(m, caller));

    /// <summary>The tools that change data, found once.</summary>
    private static readonly MethodInfo[] ChangingDataTools = ChangingData.SelectMany(Methods).ToArray();

    /// <summary>
    /// Every tool as a chat model's function, made once (its schema read from the method, its class's factory worked out): each call
    /// runs as the caller its arguments carry (<see cref="InAScopeOfItsOwn"/> binds it), so a reply only binds its caller.
    /// </summary>
    private static readonly (MethodInfo Method, AIFunction Function, ToolEffect Effect)[] Functions = All.SelectMany(set =>
        {
            var create = Factory(set.Type);
            return Methods(set.Type).Select(method => (method, (AIFunction)AIFunctionFactory.Create(method,
                args => create(args.Services!, (ToolCaller)args.Context![typeof(ToolCaller)]!),
                new AIFunctionFactoryOptions { Name = Name(method) }), set.Effect));
        })
        .ToArray();

    /// <summary>
    /// The tag on a chat tool call's <c>execute_tool</c> span that says how it went: <c>ok</c>, <c>refused</c> (a
    /// <see cref="ToolException"/>, its result: no error), <c>canceled</c> (the reply was: the asker stopped waiting, or it took too
    /// long) or <c>failed</c> (any other exception, a tool's own timeout too).
    /// </summary>
    public const string OutcomeTag = "grif.tool.outcome";

    /// <summary>
    /// The tag on a failed chat tool call's <c>execute_tool</c> span that names the failure's own type: its <c>error.type</c> is
    /// <see cref="ToolFailedException"/>'s when the failure's message is left out of the telemetry.
    /// </summary>
    public const string ErrorTypeTag = "grif.tool.error.type";

    /// <summary>
    /// A function run as <paramref name="caller"/>, in a new scope per call, whose <see cref="ToolException"/> is its result, told as a
    /// refusal. Each call is logged to <paramref name="log"/> (its tool, outcome and time; not its arguments or result, which are what
    /// people asked for) and its outcome tagged on its span. A failure's exception is the function invocation's own log: without
    /// <paramref name="sensitiveData"/>, as a <see cref="ToolFailedException"/>, its type and stack without its message.
    /// </summary>
    private sealed class InAScopeOfItsOwn(AIFunction inner, IServiceScopeFactory scopes, ToolCaller caller, ILogger log, bool sensitiveData)
        : DelegatingAIFunction(inner)
    {
        private const string Ok = "ok";
        private const string Refused = "refused";
        private const string Canceled = "canceled";
        private const string Failed = "failed";

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            arguments.Services = scope.ServiceProvider;
            arguments.Context ??= new Dictionary<object, object?>();
            arguments.Context[typeof(ToolCaller)] = caller;
            var started = Stopwatch.GetTimestamp();
            var outcome = Failed;
            string? failure = null;
            try
            {
                var result = await base.InvokeCoreAsync(arguments, cancellationToken);
                outcome = Ok;
                return result;
            }
            catch (ToolException e)
            {
                outcome = Refused;
                return $"Refused: {e.Message}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The reply's, not the tool's own (an HTTP client's timeout is a failure)
                outcome = Canceled;
                throw;
            }
            catch (Exception e)
            {
                failure = e.GetType().FullName;
                if (sensitiveData)
                    throw;
                throw new ToolFailedException(e);
            }
            finally
            {
                // The span's own duration isn't known until it ends, after this: the log's is measured here
                Record(outcome, failure, Stopwatch.GetElapsedTime(started));
            }
        }

        private void Record(string outcome, string? failure, TimeSpan elapsed)
        {
            // The function invocation's execute_tool span for this call, when the chat is traced (else the span the reply runs
            // under, which isn't this call's)
            if (Activity.Current is { } span && Equals(span.GetTagItem("gen_ai.operation.name"), "execute_tool"))
            {
                span.SetTag(OutcomeTag, outcome);
                span.SetTag(ErrorTypeTag, failure);
            }
            ToolCallLog.Called(log, Name, outcome, elapsed.TotalMilliseconds, caller.UserID);
        }
    }
}

/// <summary>
/// A chat tool's failure, as the telemetry is told it when what people wrote is left out of it (Chat:Telemetry:SensitiveData): the
/// failure's type and where it was thrown, not its message, which may quote the tool's arguments.
/// </summary>
public sealed class ToolFailedException(Exception failed) : Exception(failed.GetType().FullName)
{
    /// <summary>The failure's own stack.</summary>
    public override string? StackTrace => failed.StackTrace;
}

/// <summary>A chat tool call's log line (<see cref="GrifTools.AIFunctions"/>): its tool, outcome and time, and who called it.</summary>
internal static partial class ToolCallLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Chat tool {Tool}: {Outcome} in {ElapsedMs} ms, for user {UserID}")]
    public static partial void Called(ILogger logger, string tool, string outcome, double elapsedMs, int? userID);
}
