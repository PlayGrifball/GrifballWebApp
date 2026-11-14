using GrifballWebApp.Application.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GrifballWebApp.Server.Seasons;

/// <summary>
/// Every action that names a season, a team or a season match (an argument called seasonID, teamID or seasonMatchID, the
/// routes' names) answers 404 for one of a season the caller may not see: one not announced yet, to anyone but a
/// commissioner or sysadmin (#435). The same answer as for no such season, so a hidden one can't be told apart from none.
/// </summary>
public sealed class SeasonVisibilityFilter : IAsyncActionFilter
{
    public const string NotFound = "There is no such season.";

    private readonly SeasonVisibility _visibility;

    public SeasonVisibilityFilter(SeasonVisibility visibility)
    {
        _visibility = visibility;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var viewer = SeasonVisibility.For(context.HttpContext.User);
        if (viewer is SeasonViewer.Public && await Hidden(context.ActionArguments, viewer, context.HttpContext.RequestAborted))
        {
            context.Result = ((ControllerBase)context.Controller).NotFoundProblem(NotFound);
            return;
        }
        await next();
    }

    private async Task<bool> Hidden(IDictionary<string, object?> arguments, SeasonViewer viewer, CancellationToken ct)
    {
        foreach (var (name, value) in arguments)
        {
            if (value is not int id)
                continue;
            var hidden = name.ToLowerInvariant() switch
            {
                "seasonid" => await _visibility.IsSeasonHidden(id, viewer, ct),
                "teamid" => await _visibility.IsTeamHidden(id, viewer, ct),
                "seasonmatchid" => await _visibility.IsSeasonMatchHidden(id, viewer, ct),
                _ => false,
            };
            if (hidden)
                return true;
        }
        return false;
    }
}
