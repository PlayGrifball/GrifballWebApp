using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;

public class VodTeamConfiguration : IEntityTypeConfiguration<VodTeam>
{
    public void Configure(EntityTypeBuilder<VodTeam> entity)
    {
        entity.ToTable("VodTeams", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.VodID, e.TeamID });

        // VODs are never deleted (a dismissal is remembered); cascading here too would be a second path from SeasonMatches
        entity.HasOne(e => e.Vod)
            .WithMany(v => v.Teams)
            .HasForeignKey(e => e.VodID)
            .OnDelete(DeleteBehavior.ClientCascade);

        // The import removes teams gone from a sheet: their VOD links go with them
        entity.HasOne(e => e.Team)
            .WithMany()
            .HasForeignKey(e => e.TeamID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
