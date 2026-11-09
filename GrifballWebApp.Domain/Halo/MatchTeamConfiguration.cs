using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Halo;
public partial class MatchTeamConfiguration : IEntityTypeConfiguration<MatchTeam>
{
    /// <summary>An outcome the model knows (<see cref="Outcomes"/>; Match.FromHalo refuses any other).</summary>
    public static readonly string KnownOutcome = $"Outcome IN ({string.Join(", ", Enum.GetValues<Outcomes>().Select(o => (int)o))})";

    public void Configure(EntityTypeBuilder<MatchTeam> entity)
    {
        entity.ToTable("MatchTeams", "Infinite", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Infinite_MatchTeams_KnownOutcome", KnownOutcome);
        });

        entity.HasKey(e => new { e.MatchID, e.TeamID });

        entity.HasOne(d => d.Match)
            .WithMany(p => p.MatchTeams)
            .HasForeignKey(d => d.MatchID);

        // There can only be one team 0, 1, etc. Index should only be needed if we change PK
        //entity.HasIndex(e => new { e.TeamID, e.MatchID }).IsUnique();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MatchTeam> entity);
}
