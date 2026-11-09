using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;
public partial class SeasonSignupConfiguration : IEntityTypeConfiguration<SeasonSignup>
{
    /// <summary>A team name is stored trimmed, or not at all (SeasonSignup.SignUp, Change): never blank.</summary>
    public const string TeamNameTrimmed = "TeamName IS NULL OR (TeamName <> '' AND TeamName = LTRIM(RTRIM(TeamName)))";

    /// <summary>Only a captain asks for help drafting. Booleans compared as numbers, which all three databases take.</summary>
    public const string AssistanceOnlyForCaptains = "CAST(RequiresAssistanceDrafting AS INT) = 0 OR CAST(WillCaptain AS INT) = 1";

    public void Configure(EntityTypeBuilder<SeasonSignup> entity)
    {
        entity.ToTable("SeasonSignups", "Event", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Event_SeasonSignups_TeamNameTrimmed", TeamNameTrimmed);
            tb.HasCheckConstraint("CK_Event_SeasonSignups_AssistanceOnlyForCaptains", AssistanceOnlyForCaptains);
        });

        entity.HasKey(e => e.SeasonSignupID);

        entity.HasIndex(e => new { e.UserID, e.SeasonID }).IsUnique();

        entity.HasOne(d => d.Season)
            .WithMany(p => p.SeasonSignups)
            .HasForeignKey(d => d.SeasonID);

        entity.HasOne(d => d.User)
            .WithMany(p => p.SeasonSignups)
            .HasForeignKey(d => d.UserID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonSignup> entity);
}
