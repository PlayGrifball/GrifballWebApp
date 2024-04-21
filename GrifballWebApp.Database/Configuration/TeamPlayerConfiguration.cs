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

        // Player can not be on the same team twice, ideally player would not be on two teams either but I lack seasonID to enforce it.
        entity.HasIndex(e => new { e.UserID, e.TeamID }).IsUnique();

        entity.HasOne(d => d.Team)
            .WithMany(p => p.TeamPlayers)
            .HasForeignKey(d => d.TeamID);

        entity.HasOne(d => d.User)
            .WithMany(p => p.TeamPlayers)
            .HasForeignKey(d => d.UserID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamPlayer> entity);
}
