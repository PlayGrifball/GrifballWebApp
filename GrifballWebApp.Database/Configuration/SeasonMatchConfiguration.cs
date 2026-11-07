using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonMatchConfiguration : IEntityTypeConfiguration<SeasonMatch>
{
    private const int Won = (int)SeasonMatchResult.Won;
    private const int Loss = (int)SeasonMatchResult.Loss;
    private const int Forfeit = (int)SeasonMatchResult.Forfeit;
    private const int Bye = (int)SeasonMatchResult.Bye;

    /// <summary>
    /// The 11 legal (home, away) result pairs (see <see cref="SeasonMatch"/>): undecided; decided by its games (Won, Loss);
    /// forfeited (Won, Forfeit) or by both; a BYE against nothing yet, against a BYE, or won by the team facing it. A
    /// missing result is -1 here, so no comparison is ever unknown (a check passes a row it can't decide).
    /// </summary>
    public static readonly string LegalResults =
        $"({Home} = {None} AND {Away} = {None})"
        + $" OR ({Home} = {Won} AND {Away} IN ({Loss}, {Forfeit}, {Bye}))"
        + $" OR ({Home} = {Loss} AND {Away} = {Won})"
        + $" OR ({Home} = {Forfeit} AND {Away} IN ({Won}, {Forfeit}))"
        + $" OR ({Home} = {Bye} AND {Away} IN ({None}, {Won}, {Bye}))"
        + $" OR ({Home} = {None} AND {Away} = {Bye})";

    private const int None = -1;
    private const string Home = "COALESCE(HomeTeamResult, -1)";
    private const string Away = "COALESCE(AwayTeamResult, -1)";

    /// <summary>A BYE side has no team: no team ever plays there.</summary>
    public static readonly string ByeHasNoTeam =
        $"(HomeTeamResult IS NULL OR HomeTeamResult <> {Bye} OR HomeTeamID IS NULL)"
        + $" AND (AwayTeamResult IS NULL OR AwayTeamResult <> {Bye} OR AwayTeamID IS NULL)";

    public void Configure(EntityTypeBuilder<SeasonMatch> entity)
    {
        entity.ToTable("SeasonMatches", "Event", tb =>
        {
            tb.IsTemporal();

            // Make sure that at least 1 team is null. If both are not null then they must not be the same
            tb.HasCheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams", @"
(HomeTeamID IS NULL) OR
(AwayTeamID IS NULL) OR
(HomeTeamID != AwayTeamID)
".Replace(Environment.NewLine, " ").Trim()); // Do not put new lines in SQL constraints, it will cause PendingModelChangesWarning

            // Each side's result pairs with the other's only in the legal ways, and a BYE side has no team
            tb.HasCheckConstraint("CK_Event_SeasonMatches_LegalResults", LegalResults);
            tb.HasCheckConstraint("CK_Event_SeasonMatches_ByeHasNoTeam", ByeHasNoTeam);
        });

        entity.HasKey(e => e.SeasonMatchID);

        // Worked out from its results and teams, not stored: without these, EF would map them
        entity.Ignore(e => e.IsDecided);
        entity.Ignore(e => e.DecidedBy);
        entity.Ignore(e => e.Winner);
        entity.Ignore(e => e.Loser);
        entity.Ignore(e => e.WinnerTeamID);
        entity.Ignore(e => e.LoserTeamID);
        entity.Ignore(e => e.HasBye);
        entity.Ignore(e => e.HasBothTeams);
        entity.Ignore(e => e.CanReopen);

        entity.HasOne(d => d.Season)
            .WithMany(p => p.SeasonMatches)
            .HasForeignKey(d => d.SeasonID);

        // TODO: Determine correct delete behavior.

        entity.HasOne(d => d.AwayTeam)
            .WithMany(p => p.AwayMatches)
            .HasForeignKey(d => d.AwayTeamID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(d => d.HomeTeam)
            .WithMany(p => p.HomeMatches)
            .HasForeignKey(d => d.HomeTeamID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(x => x.ActiveRescheduleRequest)
            .WithOne()
            .HasForeignKey<SeasonMatch>(x => x.ActiveRescheduleRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonMatch> entity);
}
