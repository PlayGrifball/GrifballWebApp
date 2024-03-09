using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class PersonExperienceConfiguration : IEntityTypeConfiguration<PersonExperience>
{
    public void Configure(EntityTypeBuilder<PersonExperience> entity)
    {
        entity.ToTable("PersonExperiences", "ITS", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.PersonID, e.GameVersionID });

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<PersonExperience> entity);
}
