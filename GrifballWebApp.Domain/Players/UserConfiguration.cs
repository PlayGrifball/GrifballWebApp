using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int ByUser = (int)RegionSource.User;
    private const int BySysadmin = (int)RegionSource.Sysadmin;
    private const int ByImport = (int)RegionSource.Import;

    /// <summary>Who chose a region is one the model knows.</summary>
    public static readonly string KnownRegionSource = $"RegionSource IS NULL OR RegionSource IN ({ByUser}, {BySysadmin}, {ByImport})";

    /// <summary>A region is set with who chose it, and only then (User.ChooseRegion).</summary>
    public const string RegionHasSource = "(RegionID IS NULL AND RegionSource IS NULL) OR (RegionID IS NOT NULL AND RegionSource IS NOT NULL)";

    /// <summary>
    /// The Late League record (User.RecordLateLeagueWin, RecordLateLeagueLoss): nothing negative, one streak at a time, and
    /// no streak longer than the wins or losses it is made of.
    /// </summary>
    public const string LateLeagueRecord = "MMR >= 0 AND Wins >= 0 AND Losses >= 0 AND WinStreak >= 0 AND LossStreak >= 0"
        + " AND (WinStreak = 0 OR LossStreak = 0) AND WinStreak <= Wins AND LossStreak <= Losses";

    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users", "Auth", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Auth_Users_KnownRegionSource", KnownRegionSource);
            tb.HasCheckConstraint("CK_Auth_Users_RegionHasSource", RegionHasSource);
            tb.HasCheckConstraint("CK_Auth_Users_LateLeagueRecord", LateLeagueRecord);
        });

        entity.Property(e => e.DisplayName).HasMaxLength(User.DisplayNameMaxLength);

        // Worked out from the user name, not stored
        entity.Ignore(e => e.IsImportPlaceholder);

        entity.HasOne(d => d.Region)
            .WithMany(p => p.Users)
            .HasForeignKey(d => d.RegionID);

        entity.HasOne(x => x.QueuedPlayer)
            .WithOne(x => x.User)
            .HasForeignKey<QueuedPlayer>(x => x.UserID)
            .IsRequired(false);

        // Restrict: a player leaves a match only through it (MatchedMatch), never with its user; a merge moves them first
        entity.HasMany(x => x.MatchedPlayers)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
