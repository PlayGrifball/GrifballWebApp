using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Application.Tools;

/// <summary>The season or season match a staff tool names, else the season its purpose means: refused, when there is none, with a reason the model can act on.</summary>
internal static class StaffSeasons
{
    /// <summary>The season asked for, if it's there (and the caller may see it), else the one <paramref name="purpose"/> means.</summary>
    public static async Task<int> SeasonOrDefault(SeasonService seasons, SeasonVisibility visibility, ToolCaller caller, int? seasonID, SeasonPurpose purpose, CancellationToken ct)
    {
        if (seasonID is int id)
            return await visibility.CanSeeSeason(id, caller.Viewer, ct) ? id : throw new ToolException($"There is no season {id}: list_seasons has their IDs.");
        var season = await seasons.GetDefaultSeasonID(purpose, caller.Viewer, ct);
        return season != 0 ? season : throw new ToolException("There is no season to do that to: name one (list_seasons has their IDs).");
    }

    /// <summary>The season match, if it's there (and the caller may see it).</summary>
    public static async Task<int> SeasonMatch(SeasonVisibility visibility, ToolCaller caller, int seasonMatchID, CancellationToken ct)
        => await visibility.CanSeeSeasonMatch(seasonMatchID, caller.Viewer, ct) ? seasonMatchID : throw new ToolException($"There is no season match {seasonMatchID}.");
}
