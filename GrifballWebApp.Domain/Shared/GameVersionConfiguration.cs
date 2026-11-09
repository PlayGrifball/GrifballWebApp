using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Shared;

// Seeds the game versions through their seed factory (GameVersion.Seed)
[AggregateAccess(typeof(GameVersion))]
public partial class GameVersionConfiguration : IEntityTypeConfiguration<GameVersion>
{
    public const string NameAndSlugNotBlank = "LTRIM(RTRIM(GameVersionName)) <> '' AND LTRIM(RTRIM(Slug)) <> ''";

    public void Configure(EntityTypeBuilder<GameVersion> entity)
    {
        entity.ToTable("GameVersions", "Other", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Other_GameVersions_NameAndSlugNotBlank", NameAndSlugNotBlank);
        });

        entity.HasKey(e => e.GameVesionID);

        entity.Property(e => e.GameVersionName).HasMaxLength(30).IsRequired();
        entity.Property(e => e.Slug).HasMaxLength(30).IsRequired();

        entity.HasIndex(e => e.GameVersionName).IsUnique();
        entity.HasIndex(e => e.Slug).IsUnique();

        GameVersion[] gameVersions =
        {
            GameVersion.Seed("Halo 3", "halo3", gameVersionID: 1),
            GameVersion.Seed("Halo Reach", "reach", gameVersionID: 2),
            GameVersion.Seed("Halo Reach Dash", "reach-dash", gameVersionID: 3),
            GameVersion.Seed("Halo 4", "halo4", gameVersionID: 4),
            GameVersion.Seed("Halo 5", "halo5", gameVersionID: 5),
            GameVersion.Seed("Halo Infinite", "infinite", gameVersionID: 6),
        };

        entity.HasData(gameVersions);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<GameVersion> entity);
}
