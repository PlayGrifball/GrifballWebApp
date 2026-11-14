using System.Globalization;
using System.Security.Claims;
using GrifballWebApp.Application.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// The MCP server: the platform's tools (<see cref="GrifTools"/>) for AI agents such as Claude Code, over streamable HTTP at
/// <see cref="Path"/> (under /api, as the API is). A client signs in with a personal access token
/// (<see cref="AccessTokenAuthentication"/>) and acts as the user who made it, with their roles: what it reads and does
/// shows up as theirs. Stateless, so any API pod answers any request.
/// </summary>
public static class GrifMcp
{
    public const string Path = "mcp";

    public const string Instructions =
        "Grifball league platform (seasons, teams, players, matches, stats). Tools take IDs: search finds players, teams and "
        + "seasons by name, list_seasons lists seasons, and a season-scoped tool left without a seasonID uses the running season. "
        + "You act as the signed-in user (who_am_i).";

    /// <summary>The tools, their access token scheme and the MCP server's services.</summary>
    public static void AddGrifMcp(this IServiceCollection services, AuthenticationBuilder authentication)
    {
        services.AddTransient<AccessTokenService>();
        authentication.AddScheme<AuthenticationSchemeOptions, AccessTokenAuthentication>(AccessTokenAuthentication.SchemeName, null);
        services.AddAuthorizationBuilder().AddPolicy(AccessTokenAuthentication.SchemeName, policy => policy
            .AddAuthenticationSchemes(AccessTokenAuthentication.SchemeName)
            .RequireAuthenticatedUser());

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "grifball", Title = "Grifball", Version = GitInfo.CommitShortHash };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools(Tools(GrifTools.All))
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, ct) =>
            {
                try
                {
                    return await next(request, ct);
                }
                catch (ToolException e)
                {
                    // The model sees this message and can act on it; the SDK hides any other exception's
                    throw new McpException(e.Message, e);
                }
            }));
    }

    /// <summary>The endpoint, open to access tokens only (not to the browser's cookie or the API's bearer tokens).</summary>
    public static void MapGrifMcp(this IEndpointRouteBuilder app)
        => app.MapMcp(Path).RequireAuthorization(AccessTokenAuthentication.SchemeName);

    /// <summary>The caller a request's user is: the user ID and roles the access token signed in.</summary>
    public static ToolCaller Caller(ClaimsPrincipal user)
        => new(int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
            user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList());

    /// <summary>
    /// An MCP tool for each tool method of <paramref name="sets"/>, named as <see cref="GrifTools.Name"/> says, its annotations
    /// telling clients what it does (they ask before a tool that isn't read-only). Each call gets a new instance from the
    /// request's services, as the request's user (<see cref="GrifTools.Factory"/>).
    /// </summary>
    public static IEnumerable<McpServerTool> Tools(IEnumerable<(Type Type, ToolEffect Effect)> sets)
        => sets.SelectMany(set =>
        {
            var create = GrifTools.Factory(set.Type);
            return GrifTools.Methods(set.Type).Select(method => McpServerTool.Create(method,
                request => create(request.Services!, Caller(request.User!)),
                new McpServerToolCreateOptions
                {
                    Name = GrifTools.Name(method),
                    ReadOnly = set.Effect == ToolEffect.ReadsOnly,
                    // Pulling from Halo adds games; nothing is changed or deleted
                    Destructive = false,
                    // A second pull can save games played since the first
                    Idempotent = set.Effect == ToolEffect.ReadsOnly,
                    OpenWorld = set.Effect == ToolEffect.FetchesFromHalo,
                }));
        });
}
