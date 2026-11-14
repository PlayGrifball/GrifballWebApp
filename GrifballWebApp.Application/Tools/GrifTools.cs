using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace GrifballWebApp.Application.Tools;

/// <summary>The signed-in user a tool runs as: the MCP client's access token holder, with their site roles.</summary>
public sealed record ToolCaller(int UserID, IReadOnlyList<string> Roles);

/// <summary>
/// A refusal the model should see and can act on (no such season, say). The host passes its message on: the MCP server as an
/// error result. Any other exception's message stays on the server.
/// </summary>
public class ToolException(string message) : Exception(message);

/// <summary>
/// The platform's tools for AI agents: one set, for the MCP server (and the Discord bot's assistant later). A tool is a public
/// method with a [Description] on one of these classes. Each calls the same services as the controllers, and shows the caller
/// no more than the site would.
/// </summary>
public static class GrifTools
{
    /// <summary>The classes of the tools that only read, open to anyone on the site.</summary>
    public static readonly Type[] ReadOnly = [typeof(SeasonTools), typeof(TeamTools), typeof(PlayerTools), typeof(MatchTools), typeof(BrowseTools)];

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
}
