using GrifballWebApp.Application.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GrifballWebApp.Server.Seasons;

/// <summary>
/// Every action that names a season, a team or a season match (an argument called seasonID, teamID or seasonMatchID, the
/// routes' names) answers 404 to anyone but a commissioner or sysadmin for one of a season they may not see (not announced
/// yet, #435), and for one that doesn't exist alike: the same answer, so trying IDs can't find a hidden season. Staff get
/// the actions' own answers.
/// </summary>
public sealed class SeasonVisibilityFilter : IAsyncActionFilter
{
    public const string NoSuchSeason = "There is no such season.";
    public const string NoSuchTeam = "There is no such team.";
    public const string NoSuchSeasonMatch = "There is no such season match.";

    private readonly SeasonVisibility _visibility;

    public SeasonVisibilityFilter(SeasonVisibility visibility)
    {
        _visibility = visibility;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (SeasonVisibility.For(context.HttpContext.User) is SeasonViewer.Public
            && await Unseen(context.ActionArguments, context.HttpContext.RequestAborted) is { } refusal)
        {
            context.Result = ((ControllerBase)context.Controller).NotFoundProblem(refusal);
            return;
        }
        await next();
    }

    /// <summary>The 404's reason for the first season, team or season match the public may not see (or that isn't there); null for none.</summary>
    private async Task<string?> Unseen(IDictionary<string, object?> arguments, CancellationToken ct)
    {
        foreach (var (name, value) in arguments)
        {
            if (value is not int id)
                continue;
            var refusal = name.ToLowerInvariant() switch
            {
                "seasonid" => await _visibility.CanSeeSeason(id, SeasonViewer.Public, ct) ? null : NoSuchSeason,
                "teamid" => await _visibility.CanSeeTeam(id, SeasonViewer.Public, ct) ? null : NoSuchTeam,
                "seasonmatchid" => await _visibility.CanSeeSeasonMatch(id, SeasonViewer.Public, ct) ? null : NoSuchSeasonMatch,
                _ => null,
            };
            if (refusal is not null)
                return refusal;
        }
        return null;
    }
}
