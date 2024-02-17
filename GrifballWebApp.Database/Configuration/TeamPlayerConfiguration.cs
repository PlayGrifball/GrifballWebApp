using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class TeamPlayerConfiguration : IEntityTypeConfiguration<TeamPlayer>
{
    public void Configure(EntityTypeBuilder<TeamPlayer> entity)
    {
        entity.ToTable("TeamPlayers", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.TeamPlayerID);

        entity.HasOne(d => d.Team)
            .WithMany(p => p.TeamPlayers)
            .HasForeignKey(d => d.TeamID);

        entity.HasOne(d => d.Person)
            .WithMany(p => p.TeamPlayers)
            .HasForeignKey(d => d.PlayerID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamPlayer> entity);
}
