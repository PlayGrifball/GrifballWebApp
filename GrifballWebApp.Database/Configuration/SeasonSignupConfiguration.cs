using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonSignupConfiguration : IEntityTypeConfiguration<SeasonSignup>
{
    public void Configure(EntityTypeBuilder<SeasonSignup> entity)
    {
        entity.ToTable("SeasonSignups", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.SeasonSignupID);

        entity.HasIndex(e => new { e.UserID, e.SeasonID }).IsUnique();

        entity.HasOne(d => d.Season)
            .WithMany(p => p.SeasonSignups)
            .HasForeignKey(d => d.SeasonID);

        entity.HasOne(d => d.User)
            .WithMany(p => p.SeasonSignups)
            .HasForeignKey(d => d.UserID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonSignup> entity);
}
