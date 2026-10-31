using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

/// <summary>Past gamertags of one Xbox account (renames); a person's other Xbox accounts are not modelled.</summary>
public class XboxGamertagHistoryConfiguration : IEntityTypeConfiguration<XboxGamertagHistory>
{
    public void Configure(EntityTypeBuilder<XboxGamertagHistory> entity)
    {
        // With the Xbox accounts it belongs to; its row history is PastGamertagsHistory
        entity.ToTable("PastGamertags", "Xbox", tb => tb.IsTemporal());

        entity.HasKey(e => e.XboxGamertagHistoryID);

        entity.Property(e => e.Gamertag).HasMaxLength(50).IsRequired();
        entity.Property(e => e.NormalizedGamertag).HasMaxLength(50).IsRequired();

        entity.HasOne(e => e.XboxUser)
            .WithMany()
            .HasForeignKey(e => e.XboxUserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.XboxUserID, e.NormalizedGamertag }).IsUnique();
        entity.HasIndex(e => e.NormalizedGamertag);
    }
}
