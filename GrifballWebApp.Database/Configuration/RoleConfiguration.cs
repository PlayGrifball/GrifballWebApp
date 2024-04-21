using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("Roles", "Auth", tb => tb.IsTemporal());

        entity.HasKey(e => e.Id);

        entity.HasData(new List<Role>()
            {
                new Role()
                {
                    Id = 1,
                    Name = "Sysadmin",
                    NormalizedName = "SYSADMIN",
                    ConcurrencyStamp = "cda78946-53d4-4154-b723-2b33b95341cc"
                },
                new Role()
                {
                    Id = 2,
                    Name = "Commissioner",
                    NormalizedName = "COMMISSIONER",
                    ConcurrencyStamp = "37ff5fdf-5cb8-403c-bb9e-81db05a5fcdd"
                },
                new Role()
                {
                    Id = 3,
                    Name = "Player",
                    NormalizedName = "PLAYER",
                    ConcurrencyStamp = "0be992c7-f824-40c7-8975-964068c7fbfe"
                },
            });

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Role> entity);
}
