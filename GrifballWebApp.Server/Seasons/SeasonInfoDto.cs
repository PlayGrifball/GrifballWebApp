using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Seasons;

/// <summary>
/// What a season is, the same on every page that shows one, imported from the historical archive or played in the app
/// alike. A DTO that is a season (<see cref="SeasonSummaryDto"/>, <see cref="SeasonListItemDto"/>) derives from it and is
/// projected by <see cref="Select{T}"/>; one about a season (a stat line, a title, a team) has it as its Season, loaded
/// by <see cref="LoadAsync"/>. Either way the fields are projected here only.
/// </summary>
public class SeasonInfoDto
{
    public int SeasonID { get; set; }
    public string SeasonName { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Year { get; set; }
    /// <summary>The season is over: a section with nothing to show says it wasn't recorded.</summary>
    public bool HasEnded { get; set; }
    public int LeagueID { get; set; }
    public string LeagueName { get; set; } = "";
    public string LeagueSlug { get; set; } = "";
    /// <summary>The season's game; null for one played on more than one (a stat line or title has its own).</summary>
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public string? Division { get; set; }
    /// <summary>Which sections the season's page has: a tournament has no schedule or standings, a Late League season no
    /// playoffs, bracket or champion.</summary>
    public SeasonFormat Format { get; set; }
    /// <summary>Imported from the historical archive: <see cref="HistoricalSourceUrl"/> is its source spreadsheet.</summary>
    public bool IsHistorical { get; set; }
    public string? HistoricalSourceUrl { get; set; }

    private static readonly Expression<Func<Season, SeasonInfoDto>> Fields = s => new SeasonInfoDto
    {
        SeasonID = s.SeasonID,
        SeasonName = s.SeasonName,
        Start = s.SeasonStart,
        End = s.SeasonEnd,
        Year = s.SeasonStart.Year,
        HasEnded = s.SeasonEnd < DateTime.UtcNow,
        LeagueID = s.LeagueID,
        LeagueName = s.League.LeagueName,
        LeagueSlug = s.League.Slug,
        GameVersionID = s.GameVersionID,
        GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : null,
        GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : null,
        Division = s.Division,
        Format = s.Format,
        IsHistorical = s.HistoricalSourceID != null,
        HistoricalSourceUrl = s.HistoricalSourceUrl,
    };

    public static readonly Expression<Func<Season, SeasonInfoDto>> Of = Fields;

    /// <summary>
    /// The projection of a DTO that is a season: the members <paramref name="extra"/> sets (a member initializer, e.g.
    /// <c>s =&gt; new SeasonListItemDto { TeamCount = s.Teams.Count }</c>) and the season's own from <see cref="Of"/>, as
    /// one expression the database translates.
    /// </summary>
    public static Expression<Func<Season, T>> Select<T>(Expression<Func<Season, T>> extra) where T : SeasonInfoDto
    {
        var (newT, bindings) = extra.Body switch
        {
            MemberInitExpression init => (init.NewExpression, init.Bindings),
            NewExpression created => (created, (IEnumerable<MemberBinding>)[]),
            _ => throw new ArgumentException("Expected a member initializer.", nameof(extra)),
        };
        var parameter = extra.Parameters[0];
        var fields = ((MemberInitExpression)new ReplaceParameter(Fields.Parameters[0], parameter).Visit(Fields.Body)).Bindings;
        return Expression.Lambda<Func<Season, T>>(Expression.MemberInit(newT, fields.Concat(bindings)), parameter);
    }

    /// <summary>The seasons' facts by SeasonID, for the DTOs about a season.</summary>
    public static async Task<Dictionary<int, SeasonInfoDto>> LoadAsync(GrifballContext context, IEnumerable<int> seasonIDs, CancellationToken ct)
    {
        var ids = seasonIDs.Distinct().ToList();
        if (ids.Count == 0)
            return [];
        return await context.Seasons.AsNoTracking().Where(s => ids.Contains(s.SeasonID)).Select(Of).ToDictionaryAsync(s => s.SeasonID, ct);
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
