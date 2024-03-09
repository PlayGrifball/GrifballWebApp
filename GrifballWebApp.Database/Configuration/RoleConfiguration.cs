using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("Roles", "ITS", tb => tb.IsTemporal());

        entity.HasKey(e => e.RoleID);

        entity.HasIndex(e => e.Name).IsUnique();

        entity.Property(e => e.Name)
            .HasConversion<string>();

        var roles = Enum.GetValues<RoleNames>()
            .Select(x => new Role()
            {
                RoleID = (int)x,
                Name = x,
            }).ToArray();

        entity.HasData(roles);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Role> entity);
}
