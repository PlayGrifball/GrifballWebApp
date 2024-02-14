using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class TeamPlayerConfiguration : IEntityTypeConfiguration<TeamPlayer>
{
    public void Configure(EntityTypeBuilder<TeamPlayer> entity)
    {
        entity.ToTable("Event", "TeamPlayers", tb => tb.IsTemporal());

        entity.HasKey(e => e.TeamPlayerID);

        entity.HasOne(d => d.Team)
            .WithMany(p => p.TeamPlayers)
            .HasForeignKey(d => d.TeamID);

        entity.HasOne(d => d.XboxUser)
            .WithMany(p => p.TeamPlayers)
            .HasForeignKey(d => d.XboxUserID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamPlayer> entity);
}
