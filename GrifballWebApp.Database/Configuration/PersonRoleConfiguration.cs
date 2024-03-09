using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class PersonRoleConfiguration : IEntityTypeConfiguration<PersonRole>
{
    public void Configure(EntityTypeBuilder<PersonRole> entity)
    {
        entity.ToTable("PersonRoles", "ITS", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.PersonID, e.RoleID });

        entity.HasOne(d => d.Person)
            .WithMany(p => p.PersonRoles)
            .HasForeignKey(d => d.PersonID);

        entity.HasOne(d => d.Role)
            .WithMany(p => p.PersonRoles)
            .HasForeignKey(d => d.RoleID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PersonRole> entity);
}
