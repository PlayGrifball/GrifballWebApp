using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> entity)
    {
        entity.ToTable("Persons", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.PersonID);

        entity.Property(e => e.Name).HasMaxLength(30).IsRequired();

        entity.HasIndex(e => e.Name).IsUnique();

        entity.HasOne(d => d.Password)
            .WithOne(d => d.Person)
            .HasForeignKey<Password>(d => d.PersonID)
            .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Person> entity);
}
