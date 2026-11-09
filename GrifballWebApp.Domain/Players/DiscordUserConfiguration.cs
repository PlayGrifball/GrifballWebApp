using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;
public partial class DiscordUserConfiguration : IEntityTypeConfiguration<DiscordUser>
{
    /// <summary>A username to show: never blank (<see cref="DiscordUser.IsUsername"/>).</summary>
    public const string UsernameNotBlank = "TRIM(DiscordUsername) <> ''";

    public void Configure(EntityTypeBuilder<DiscordUser> entity)
    {
        entity.ToTable("Discord", "User", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_User_Discord_UsernameNotBlank", UsernameNotBlank);
        });

        entity.HasKey(e => e.DiscordUserID);

        // Value always comes from discord
        entity.Property(e => e.DiscordUserID).ValueGeneratedNever();

        entity.HasOne(d => d.User)
            .WithOne(p => p.DiscordUser)
            .HasForeignKey<User>(d => d.DiscordUserID)
            .IsRequired(false);
    }
}
