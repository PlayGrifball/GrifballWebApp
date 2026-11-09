using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Shared;
public class RankConfiguration : IEntityTypeConfiguration<Rank>
{
    public const string ThresholdNotNegative = "MmrThreshold >= 0";

    public void Configure(EntityTypeBuilder<Rank> builder)
    {
        builder.ToTable("Ranks", "Matchmaking", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Matchmaking_Ranks_ThresholdNotNegative", ThresholdNotNegative);
        });

        builder.HasKey(r => r.Id);
    }
}
