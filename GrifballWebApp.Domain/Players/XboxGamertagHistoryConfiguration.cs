using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

/// <summary>Past gamertags of one Xbox account (renames); a person's other Xbox accounts are not modelled.</summary>
public class XboxGamertagHistoryConfiguration : IEntityTypeConfiguration<XboxGamertagHistory>
{
    /// <summary>A source the model knows (<see cref="GamertagHistorySource"/>).</summary>
    public static readonly string KnownSource = $"Source IN ({string.Join(", ", Enum.GetValues<GamertagHistorySource>().Select(s => (int)s))})";

    public void Configure(EntityTypeBuilder<XboxGamertagHistory> entity)
    {
        // With the Xbox accounts it belongs to; its row history is PastGamertagsHistory
        entity.ToTable("PastGamertags", "Xbox", tb =>
        {
            tb.IsTemporal();
            // First seen no later than last seen, when both are known (XboxUser.RecordPastGamertag, XboxGamertagHistory.Widen)
            tb.HasCheckConstraint("CK_Xbox_PastGamertags_SeenInOrder",
                "FirstSeen IS NULL OR LastSeen IS NULL OR FirstSeen <= LastSeen");
            tb.HasCheckConstraint("CK_Xbox_PastGamertags_KnownSource", KnownSource);
        });

        entity.HasKey(e => e.XboxGamertagHistoryID);

        entity.Property(e => e.Gamertag).HasMaxLength(50).IsRequired();
        entity.Property(e => e.NormalizedGamertag).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(XboxGamertagHistory.MaxNoteLength);
        entity.Ignore(e => e.IsRemovable);

        // Made and removed only through its account (XboxUser.PastGamertags): one taken out of it is deleted
        entity.HasOne(e => e.XboxUser)
            .WithMany(x => x.PastGamertags)
            .HasForeignKey(e => e.XboxUserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.XboxUserID, e.NormalizedGamertag }).IsUnique();
        entity.HasIndex(e => e.NormalizedGamertag);
    }
}
