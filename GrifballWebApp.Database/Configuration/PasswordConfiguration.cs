using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class PasswordConfiguration : IEntityTypeConfiguration<Password>
{
    public void Configure(EntityTypeBuilder<Password> entity)
    {
        entity.ToTable("Passwords", "Account", tb => tb.IsTemporal());

        entity.HasKey(e => e.PersonID);

        //entity.HasOne(d => d.Person)
        //    .WithOne(d => d.Password)
        //    .HasForeignKey<Password>(d => d.PersonID)
        //    .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Password> entity);
}
