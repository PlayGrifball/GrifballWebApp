using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class GameVersionConfiguration : IEntityTypeConfiguration<GameVersion>
{
    public void Configure(EntityTypeBuilder<GameVersion> entity)
    {
        entity.ToTable("GameVersions", "Other", tb => tb.IsTemporal());

        entity.HasKey(e => e.GameVesionID);

        entity.Property(e => e.GameVersionName).HasMaxLength(30).IsRequired();

        entity.HasIndex(e => e.GameVersionName).IsUnique();

        GameVersion[] gameVersions =
        {
            new()
            {
                GameVesionID = 1,
                GameVersionName = "Halo 3",
            },
            new()
            {
                GameVesionID = 2,
                GameVersionName = "Halo Reach",
            },
            new()
            {
                GameVesionID = 3,
                GameVersionName = "Halo Reach Dash",
            },
            new()
            {
                GameVesionID = 4,
                GameVersionName = "Halo 4",
            },
            new()
            {
                GameVesionID = 5,
                GameVersionName = "Halo 5",
            },
            new()
            {
                GameVesionID = 6,
                GameVersionName = "Halo Infinite",
            },
        };

        entity.HasData(gameVersions);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<GameVersion> entity);
}
