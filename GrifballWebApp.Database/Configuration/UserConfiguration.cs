using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users", "Auth", tb => tb.IsTemporal());

        entity.Property(e => e.DisplayName).HasMaxLength(30);

        entity.HasOne(d => d.Region)
            .WithMany(p => p.Users)
            .HasForeignKey(d => d.RegionID);

        entity.HasOne(x => x.QueuedPlayer)
            .WithOne(x => x.User)
            .HasForeignKey<QueuedPlayer>(x => x.UserID)
            .IsRequired(false);

        entity.HasMany(x => x.MatchedPlayers)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserID);
    }
}
