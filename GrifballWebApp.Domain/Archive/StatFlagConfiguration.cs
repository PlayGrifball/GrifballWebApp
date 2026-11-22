using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Archive;

public class StatFlagConfiguration : IEntityTypeConfiguration<StatFlag>
{
    /// <summary>A flag is of one game: a Halo Infinite match or a recorded game, never both or neither (StatFlag.Found).</summary>
    public const string OneGame = "(MatchID IS NOT NULL AND RecordedGameID IS NULL) OR (MatchID IS NULL AND RecordedGameID IS NOT NULL)";

    /// <summary>Who dismissed a flag is known only once it is dismissed, never the other way round.</summary>
    public const string DismissedByNeedsDismissedAt = "DismissedByUserID IS NULL OR DismissedAt IS NOT NULL";

    public void Configure(EntityTypeBuilder<StatFlag> entity)
    {
        // Not temporal, like the other background bookkeeping (OutboxMessageConfiguration): re-running the rules on a
        // game rewrites its open flags in place, so a flag's history would only grow, and nothing reads it.
        entity.ToTable("StatFlags", "Event", tb =>
        {
            tb.HasCheckConstraint("CK_Event_StatFlags_OneGame", OneGame);
            tb.HasCheckConstraint("CK_Event_StatFlags_DismissedByNeedsDismissedAt", DismissedByNeedsDismissedAt);
        });

        entity.HasKey(e => e.StatFlagID);

        entity.Property(e => e.Reason).HasMaxLength(StatFlag.ReasonMaxLength).IsRequired();

        // The staff list reads open flags, newest first
        entity.HasIndex(e => e.DismissedAt);

        entity.HasOne(e => e.Match)
            .WithMany()
            .HasForeignKey(e => e.MatchID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        entity.HasOne(e => e.RecordedGame)
            .WithMany()
            .HasForeignKey(e => e.RecordedGameID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // PlayerUserID and DismissedByUserID are plain columns, no foreign key (StatFlag's own doc comment): a user
        // merged away later leaves the flag as it found them, like DuplicateDismissal's user IDs.
    }
}
