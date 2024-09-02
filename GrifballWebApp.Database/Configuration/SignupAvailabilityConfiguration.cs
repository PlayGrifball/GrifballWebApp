using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SignupAvailabilityConfiguration : IEntityTypeConfiguration<SignupAvailability>
{
    public void Configure(EntityTypeBuilder<SignupAvailability> entity)
    {
        entity.ToTable("SignupAvailability", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.SeasonSignupID, e.AvailabilityOptionID });

        entity.HasOne(d => d.SeasonSignup)
            .WithMany(p => p.SignupAvailability)
            .HasForeignKey(d => d.SeasonSignupID);

        entity.HasOne(d => d.AvailabilityOption)
            .WithMany(p => p.SignupAvailability)
            .HasForeignKey(d => d.AvailabilityOptionID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SignupAvailability> entity);
}
