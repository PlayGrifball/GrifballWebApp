using GrifballWebApp.Domain.League;
#nullable disable

namespace GrifballWebApp.Domain.Shared;

/// <summary>A league seasons are played in. The app's own seasons are AGLA's (<see cref="DefaultLeagueID"/>); the
/// others are leagues whose seasons only come from the historical archive. Reference data seeded with fixed IDs
/// (LeagueConfiguration's HasData, through <see cref="Seed"/>), which the app only reads: nothing outside it sets its
/// fields, and EF uses the private setters. Its name and slug are never blank (CK_Event_Leagues_NameAndSlugNotBlank).</summary>
[AggregateRoot]
public class League : AuditableEntity
{
    /// <summary>AGLA, the league the app runs: a season made in the app is in it unless set otherwise.</summary>
    public const int DefaultLeagueID = 1;

    /// <summary>For EF. A league is seeded (<see cref="Seed"/>).</summary>
    private League() { }

    /// <summary>A league of the seed data (HasData), or of a test.</summary>
    internal static League Seed(string leagueName, string slug, int leagueID = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leagueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return new() { LeagueID = leagueID, LeagueName = leagueName, Slug = slug };
    }

    // The key is internal: the seed data gives it, and tests may give one
    public int LeagueID { get; internal set; }
    public string LeagueName { get; private set; }
    public string Slug { get; private set; }

    // Read-only: a season names its league (EF uses the field)
    public IReadOnlyCollection<Season> Seasons => _seasons;
    private readonly HashSet<Season> _seasons = [];
}
