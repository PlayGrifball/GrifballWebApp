using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users", "Auth", tb => tb.IsTemporal());

        //entity.HasKey(e => e.Id);

        entity.Property(e => e.DisplayName).HasMaxLength(30);

        //entity.HasOne(d => d.Password)
        //    .WithOne(d => d.Person)
        //    .HasForeignKey<Password>(d => d.PersonID)
        //    .IsRequired(false);

        entity.HasOne(d => d.Region)
            .WithMany(p => p.Users)
            .HasForeignKey(d => d.RegionID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<User> entity);
}
