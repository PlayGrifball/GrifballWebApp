using System.Linq.Expressions;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
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
    /// <summary>Which sections the season's page has: a tournament has no schedule or standings.</summary>
    public SeasonFormat Format { get; set; }
    /// <summary>Imported from the historical archive: <see cref="HistoricalSourceUrl"/> is its source spreadsheet.</summary>
    public bool IsHistorical { get; set; }
    public string? HistoricalSourceUrl { get; set; }

    /// <summary>The season's own fields but the rules of the model (<see cref="Of"/>): <see cref="HasEnded"/>, which
    /// depends on the time, and <see cref="IsHistorical"/>.</summary>
    private static readonly Expression<Func<Season, SeasonInfoDto>> Fields = s => new SeasonInfoDto
    {
        SeasonID = s.SeasonID,
        SeasonName = s.SeasonName,
        Start = s.SeasonStart,
        End = s.SeasonEnd,
        Year = s.SeasonStart.Year,
        LeagueID = s.LeagueID,
        LeagueName = s.League.LeagueName,
        LeagueSlug = s.League.Slug,
        GameVersionID = s.GameVersionID,
        GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : null,
        GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : null,
        Division = s.Division,
        Format = s.Format,
        HistoricalSourceUrl = s.HistoricalSourceUrl,
    };

    /// <summary>The season as a <see cref="SeasonInfoDto"/> as of <paramref name="now"/>.</summary>
    public static Expression<Func<Season, SeasonInfoDto>> Of(DateTime now)
    {
        // HasEnded is Season.EndedAt and IsHistorical Season.ImportedFromArchive, written into the projection (a query can't
        // call an expression)
        return Projections.Extend(Projections.Extend(Fields, Bound(nameof(HasEnded), Season.EndedAt(now))),
            Bound(nameof(IsHistorical), Season.ImportedFromArchive));
    }

    /// <summary>The projection that sets only <paramref name="member"/>, to <paramref name="rule"/>.</summary>
    private static Expression<Func<Season, SeasonInfoDto>> Bound(string member, Expression<Func<Season, bool>> rule)
        => Expression.Lambda<Func<Season, SeasonInfoDto>>(Expression.MemberInit(Expression.New(typeof(SeasonInfoDto)),
            Expression.Bind(typeof(SeasonInfoDto).GetProperty(member)!, rule.Body)), rule.Parameters);

    /// <summary>
    /// The projection of a DTO that is a season: the members <paramref name="extra"/> sets (a member initializer, e.g.
    /// <c>s =&gt; new SeasonListItemDto { TeamCount = s.Teams.Count }</c>) and the season's own from <see cref="Of"/>, as
    /// one expression the database translates.
    /// </summary>
    public static Expression<Func<Season, T>> Select<T>(DateTime now, Expression<Func<Season, T>> extra) where T : SeasonInfoDto
        => Projections.Extend(Of(now), extra);

    /// <summary>The seasons' facts by SeasonID, for the DTOs about a season.</summary>
    public static async Task<Dictionary<int, SeasonInfoDto>> LoadAsync(GrifballContext context, IEnumerable<int> seasonIDs, DateTime now, CancellationToken ct)
    {
        var ids = seasonIDs.Distinct().ToList();
        if (ids.Count == 0)
            return [];
        return await context.Seasons.AsNoTracking().Where(s => ids.Contains(s.SeasonID)).Select(Of(now)).ToDictionaryAsync(s => s.SeasonID, ct);
    }
}
