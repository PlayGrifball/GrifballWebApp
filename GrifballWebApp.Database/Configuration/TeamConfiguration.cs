using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> entity)
    {
        entity.ToTable("Event", "Teams", tb => tb.IsTemporal());

        entity.HasKey(e => e.TeamID);

        entity.HasOne(d => d.Season)
            .WithMany(p => p.Teams)
            .HasForeignKey(d => d.SeasonID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Team> entity);
}
