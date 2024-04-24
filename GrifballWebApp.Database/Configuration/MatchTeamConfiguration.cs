using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchTeamConfiguration : IEntityTypeConfiguration<MatchTeam>
{
    public void Configure(EntityTypeBuilder<MatchTeam> entity)
    {
        entity.ToTable("MatchTeams", "Infinite", tb => tb.IsTemporal());

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
