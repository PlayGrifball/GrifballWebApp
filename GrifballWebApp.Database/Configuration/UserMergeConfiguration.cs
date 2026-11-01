using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class UserMergeConfiguration : IEntityTypeConfiguration<UserMerge>
{
    public void Configure(EntityTypeBuilder<UserMerge> entity)
    {
        // Not temporal: it is itself the log of merges (and their undos), and the manifest can be large
        entity.ToTable("UserMerges", "Auth");

        entity.HasKey(e => e.UserMergeID);

        entity.Property(e => e.Reason).HasMaxLength(200);
        entity.Property(e => e.SourceUserName).HasMaxLength(256);
        entity.Property(e => e.TargetUserName).HasMaxLength(256);
        entity.Property(e => e.UndoNote).HasMaxLength(500);
        // nvarchar(max) / text
        entity.Property(e => e.Manifest).IsRequired();

        entity.HasIndex(e => e.MergedAt);
        entity.HasIndex(e => e.SourceUserID);
        entity.HasIndex(e => e.TargetUserID);
    }
}
