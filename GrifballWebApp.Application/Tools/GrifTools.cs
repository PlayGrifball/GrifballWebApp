using System.ComponentModel;
using System.Reflection;
using System.Text;
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
    /// <summary>Pulls fresh data from Halo and saves it before reading: adds data, never changes or deletes any.</summary>
    FetchesFromHalo,
}

/// <summary>
/// The platform's tools for AI agents: one set, for every host (the MCP server, the Discord bot, the site's chat). A tool is a
/// public method with a [Description] on one of these classes, each parameter described too. Each calls the same services as
/// the controllers, and shows the caller no more than the site would. A host makes a new instance for each call from its
/// request's services (<see cref="Create"/>).
/// </summary>
public static class GrifTools
{
    /// <summary>The classes of the tools that only read, open to anyone on the site.</summary>
    public static readonly Type[] ReadOnly = [typeof(SeasonTools), typeof(TeamTools), typeof(PlayerTools), typeof(MatchTools), typeof(BrowseTools), typeof(ClockTools)];

    /// <summary>The classes of the tools that pull from Halo first (<see cref="ToolEffect.FetchesFromHalo"/>).</summary>
    public static readonly Type[] FetchingFromHalo = [typeof(HaloTools)];

    /// <summary>Every tool class, with what its tools do.</summary>
    public static IEnumerable<(Type Type, ToolEffect Effect)> All
        => ReadOnly.Select(t => (t, ToolEffect.ReadsOnly)).Concat(FetchingFromHalo.Select(t => (t, ToolEffect.FetchesFromHalo)));

    /// <summary>
    /// The tools a chat with a small model gets (the Discord bot, the site's chat): the questions people ask most. A model picks
    /// worse from a long list, so the rest are left to the MCP server's clients.
    /// </summary>
    public static readonly string[] Chat =
    [
        "who_am_i", "get_current_time", "search", "get_season", "get_season_standings", "get_season_schedule", "get_team",
        "get_player", "get_player_rank", "get_season_match", "get_recent_games", "get_leaderboards",
    ];

    /// <summary>A class's tools: its own public instance methods with a [Description].</summary>
    public static IEnumerable<MethodInfo> Methods(Type type)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<DescriptionAttribute>() is not null);

    /// <summary>A tool's name: its method's, in snake_case (GetSeasonStandings: get_season_standings).</summary>
    public static string Name(MethodInfo method)
    {
        var name = new StringBuilder();
        foreach (var c in method.Name)
        {
            if (char.IsUpper(c) && name.Length > 0)
                name.Append('_');
            name.Append(char.ToLowerInvariant(c));
        }
        return name.ToString();
    }

    /// <summary>A tool class's instance for one call, from <paramref name="services"/>, given the caller when its constructor takes one.</summary>
    public static object Create(Type type, IServiceProvider services, ToolCaller caller)
        => type.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(ToolCaller))
            ? ActivatorUtilities.CreateInstance(services, type, caller)
            : ActivatorUtilities.CreateInstance(services, type);

    /// <summary>
    /// The tools named in <paramref name="names"/> as functions a chat model calls (Microsoft.Extensions.AI), run as
    /// <paramref name="caller"/> with <paramref name="services"/> (the host's scope). A refusal comes back as the tool's
    /// result, so the model can tell the user why; the chat client's function invocation hides other errors' details.
    /// </summary>
    public static List<AITool> AIFunctions(IServiceProvider services, ToolCaller caller, IReadOnlyCollection<string> names)
        => All.SelectMany(set => Methods(set.Type).Select(method => (set.Type, Method: method)))
            .Where(t => names.Contains(Name(t.Method)))
            .Select(t => (AITool)new RefusalsAsResults(AIFunctionFactory.Create(t.Method, _ => Create(t.Type, services, caller),
                new AIFunctionFactoryOptions { Name = Name(t.Method) })))
            .ToList();

    /// <summary>A function whose <see cref="ToolException"/> is its result, told as a refusal.</summary>
    private sealed class RefusalsAsResults(AIFunction inner) : DelegatingAIFunction(inner)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
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
