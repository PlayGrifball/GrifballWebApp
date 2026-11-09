using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;
public partial class UserExperienceConfiguration : IEntityTypeConfiguration<UserExperience>
{
    public void Configure(EntityTypeBuilder<UserExperience> entity)
    {
        entity.ToTable("UserExperiences", "Other", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.UserID, e.GameVersionID });

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<UserExperience> entity);
}
