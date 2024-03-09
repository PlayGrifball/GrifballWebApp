using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonSignupConfiguration : IEntityTypeConfiguration<SeasonSignup>
{
    public void Configure(EntityTypeBuilder<SeasonSignup> entity)
    {
        entity.ToTable("SeasonSignups", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.PersonID, e.SeasonID });

        entity.HasOne(d => d.Season)
            .WithMany(p => p.SeasonSignups)
            .HasForeignKey(d => d.SeasonID);

        entity.HasOne(d => d.Person)
            .WithMany(p => p.SeasonSignups)
            .HasForeignKey(d => d.PersonID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonSignup> entity);
}
