using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    /// <summary>A game variant is its asset and that asset's version, or neither (<see cref="Match.FromHalo"/>,
    /// <see cref="Match.GiveGameVariant"/>).</summary>
    public const string GameVariantWhole =
        "(UgcGameVariantAssetID IS NULL AND UgcGameVariantVersionID IS NULL) OR (UgcGameVariantAssetID IS NOT NULL AND UgcGameVariantVersionID IS NOT NULL)";

    /// <summary>Undecided, or a layout the model knows (<see cref="Match.Decode"/>).</summary>
    public static readonly string KnownScoreLayout = $"ScoreLayout IS NULL OR ScoreLayout IN ({string.Join(", ", Enum.GetValues<CustomScoreLayout>().Select(l => (int)l))})";

    public void Configure(EntityTypeBuilder<Match> entity)
    {
        entity.ToTable("Matches", "Infinite", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Infinite_Matches_GameVariantWhole", GameVariantWhole);
            tb.HasCheckConstraint("CK_Infinite_Matches_KnownScoreLayout", KnownScoreLayout);
        });

        entity.HasKey(e => e.MatchID);

        // Value always comes from 343
        entity.Property(e => e.MatchID).ValueGeneratedNever();

        // Worked out from its fields and its teams, not stored
        entity.Ignore(e => e.Participants);
        entity.Ignore(e => e.HasGameVariant);
        entity.Ignore(e => e.IsDecoded);

        entity.HasOne(x => x.MatchedMatch)
            .WithOne(x => x.Match)
            .HasForeignKey<MatchedMatch>(x => x.MatchID)
            .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Match> entity);
}
