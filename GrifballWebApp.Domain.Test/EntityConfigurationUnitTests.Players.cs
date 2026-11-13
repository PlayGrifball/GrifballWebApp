using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Domain.Test;

/// <summary>The Players configurations (users, their accounts, logins, merges and reset links): see the class's main file.</summary>
public partial class EntityConfigurationUnitTests
{
    [Test]
    public void UserConfiguration_TableChecksAndRelationships()
    {
        var entity = Configured(new UserConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "Users")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Auth_Users_KnownRegionSource")!.Sql,
                Is.EqualTo("RegionSource IS NULL OR RegionSource IN (1, 2, 3)"), "User, Sysadmin and Import: the region sources there are");
            Assert.That(entity.FindCheckConstraint("CK_Auth_Users_RegionHasSource")!.Sql, Is.EqualTo(UserConfiguration.RegionHasSource));
            Assert.That(entity.FindCheckConstraint("CK_Auth_Users_LateLeagueRecord")!.Sql, Is.EqualTo(
                "MMR >= 0 AND Wins >= 0 AND Losses >= 0 AND WinStreak >= 0 AND LossStreak >= 0 AND (WinStreak = 0 OR LossStreak = 0) AND WinStreak <= Wins AND LossStreak <= Losses"));
            Assert.That(Property(entity, nameof(User.DisplayName)).GetMaxLength(), Is.EqualTo(User.DisplayNameMaxLength));
            Assert.That(IsIgnored(entity, nameof(User.IsImportPlaceholder)), Is.True, "Worked out from the user name");
            var queued = ForeignKeyOf(entity, nameof(User.QueuedPlayer));
            Assert.That((queued.DeclaringEntityType.ClrType, Names(queued.Properties), queued.IsUnique),
                Is.EqualTo((typeof(QueuedPlayer), new List<string> { nameof(QueuedPlayer.UserID) }, true)), "A user is queued once at most");
            Assert.That(ForeignKeyOf(entity, nameof(User.MatchedPlayers)).DeleteBehavior, Is.EqualTo(DeleteBehavior.Restrict),
                "A player leaves a match only through it, never with its user");
            Assert.That(Names(ForeignKeyOf(entity, nameof(User.Region)).Properties), Is.EqualTo(new List<string> { nameof(User.RegionID) }));
        }
    }

    [Test]
    public void PasswordResetLinkConfiguration_TokenConcurrencyAndIndexes()
    {
        var entity = Configured(new PasswordResetLinkConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "PasswordResetLinks")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Auth_PasswordResetLinks_UsedBeforeExpiry")!.Sql, Is.EqualTo("UsedAt IS NULL OR UsedAt < ExpiresAt"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(PasswordResetLink.Id) }));
            var token = Property(entity, nameof(PasswordResetLink.Token));
            Assert.That((token.IsNullable, token.GetMaxLength()), Is.EqualTo((false, PasswordResetLink.TokenMaxLength)));
            Assert.That(Property(entity, nameof(PasswordResetLink.UsedAt)).IsConcurrencyToken, Is.True,
                "Two requests racing on one link can't both use it");
            Assert.That(Property(entity, nameof(PasswordResetLink.Token)).IsConcurrencyToken, Is.False);
            Assert.That(IndexOn(entity, nameof(PasswordResetLink.Token)).IsUnique, Is.True, "Looked up by the token's hash");
            Assert.That(IndexOn(entity, nameof(PasswordResetLink.ExpiresAt)).IsUnique, Is.False, "For the cleanup");
            Assert.That(ForeignKeyOf(entity, nameof(PasswordResetLink.User)).DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
        }
    }

    [Test]
    public void XboxGamertagHistoryConfiguration_ChecksLengthsAndIndexes()
    {
        var entity = Configured(new XboxGamertagHistoryConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Xbox", "PastGamertags")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Xbox_PastGamertags_SeenInOrder")!.Sql,
                Is.EqualTo("FirstSeen IS NULL OR LastSeen IS NULL OR FirstSeen <= LastSeen"));
            Assert.That(entity.FindCheckConstraint("CK_Xbox_PastGamertags_KnownSource")!.Sql, Is.EqualTo("Source IN (1, 2, 3, 4)"),
                "Every GamertagHistorySource, and nothing else");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(XboxGamertagHistory.XboxGamertagHistoryID) }));
            var gamertag = Property(entity, nameof(XboxGamertagHistory.Gamertag));
            var normalized = Property(entity, nameof(XboxGamertagHistory.NormalizedGamertag));
            Assert.That((gamertag.IsNullable, gamertag.GetMaxLength(), normalized.IsNullable, normalized.GetMaxLength()), Is.EqualTo((false, 50, false, 50)));
            Assert.That(Property(entity, nameof(XboxGamertagHistory.Note)).GetMaxLength(), Is.EqualTo(XboxGamertagHistory.MaxNoteLength));
            Assert.That(IsIgnored(entity, nameof(XboxGamertagHistory.IsRemovable)), Is.True);
            Assert.That(ForeignKeyOf(entity, nameof(XboxGamertagHistory.XboxUser)).DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade),
                "One taken out of its account is deleted");
            Assert.That(IndexOn(entity, nameof(XboxGamertagHistory.XboxUserID), nameof(XboxGamertagHistory.NormalizedGamertag)).IsUnique, Is.True,
                "An account's past gamertag is listed once");
            Assert.That(IndexOn(entity, nameof(XboxGamertagHistory.NormalizedGamertag)).IsUnique, Is.False, "Two accounts can have worn one gamertag");
        }
    }

    [Test]
    public void UserRoleLoginAndClaimConfigurations_KeysAndOwners()
    {
        var role = Configured(new UserRoleConfiguration());
        var login = Configured(new UserLoginConfiguration());
        var claim = Configured(new UserClaimConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(role), TableOf(login), TableOf(claim)),
                Is.EqualTo((("Auth", "UserRoles"), ("Auth", "UserLogins"), ("Auth", "UserClaims"))));
            Assert.That((role.IsTemporal(), login.IsTemporal(), claim.IsTemporal()), Is.EqualTo((true, true, true)));
            Assert.That(KeyOf(role), Is.EqualTo(new List<string> { nameof(UserRole.UserId), nameof(UserRole.RoleId) }));
            Assert.That(KeyOf(login), Is.EqualTo(new List<string> { nameof(UserLogin.LoginProvider), nameof(UserLogin.ProviderKey) }));
            Assert.That(KeyOf(claim), Is.EqualTo(new List<string> { nameof(UserClaim.Id) }), "Identity's own key, by convention");
            Assert.That(Names(ForeignKeyOf(role, nameof(UserRole.User)).Properties), Is.EqualTo(new List<string> { nameof(UserRole.UserId) }));
            Assert.That(Names(ForeignKeyOf(role, nameof(UserRole.Role)).Properties), Is.EqualTo(new List<string> { nameof(UserRole.RoleId) }));
            Assert.That(Names(ForeignKeyOf(login, nameof(UserLogin.User)).Properties), Is.EqualTo(new List<string> { nameof(UserLogin.UserId) }));
            Assert.That(Names(ForeignKeyOf(claim, nameof(UserClaim.User)).Properties), Is.EqualTo(new List<string> { nameof(UserClaim.UserId) }));
        }
    }

    [Test]
    public void UserMergeConfiguration_NotTemporalChecksAndIndexes()
    {
        var entity = Configured(new UserMergeConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "UserMerges")));
            Assert.That(entity.IsTemporal(), Is.False, "It is itself the log of merges");
            Assert.That(entity.FindCheckConstraint("CK_Auth_UserMerges_DifferentUsers")!.Sql, Is.EqualTo("SourceUserID <> TargetUserID"));
            Assert.That(entity.FindCheckConstraint("CK_Auth_UserMerges_UndoOnlyWhenUndone")!.Sql,
                Is.EqualTo("UndoneAt IS NOT NULL OR (UndoneByUserID IS NULL AND UndoNote IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Auth_UserMerges_RowCountsNotNegative")!.Sql,
                Is.EqualTo("RowsMoved >= 0 AND RowsRemoved >= 0 AND RowsAdded >= 0"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(UserMerge.UserMergeID) }));
            Assert.That(Property(entity, nameof(UserMerge.Reason)).GetMaxLength(), Is.EqualTo(UserMerge.ReasonMaxLength));
            Assert.That(Property(entity, nameof(UserMerge.SourceUserName)).GetMaxLength(), Is.EqualTo(UserMerge.UserNameMaxLength));
            Assert.That(Property(entity, nameof(UserMerge.TargetUserName)).GetMaxLength(), Is.EqualTo(UserMerge.UserNameMaxLength));
            Assert.That(Property(entity, nameof(UserMerge.UndoNote)).GetMaxLength(), Is.EqualTo(UserMerge.UndoNoteMaxLength));
            var manifest = Property(entity, nameof(UserMerge.Manifest));
            Assert.That((manifest.IsNullable, manifest.GetMaxLength()), Is.EqualTo((false, (int?)null)), "Required, and as long as it needs");
            Assert.That(IsIgnored(entity, nameof(UserMerge.IsStanding)), Is.True, "Worked out from UndoneAt");
            Assert.That(IndexOn(entity, nameof(UserMerge.MergedAt)).IsUnique, Is.False);
            Assert.That(IndexOn(entity, nameof(UserMerge.SourceUserID)).IsUnique, Is.False);
            Assert.That(IndexOn(entity, nameof(UserMerge.TargetUserID)).IsUnique, Is.False, "A user can take in several merges");
        }
    }

    [Test]
    public void DiscordAndXboxUserConfigurations_IdsFromOutsideAndOptionalUser()
    {
        var discord = Configured(new DiscordUserConfiguration());
        var xbox = Configured(new XboxUserConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(discord), TableOf(xbox)), Is.EqualTo((("User", "Discord"), ("Xbox", "XboxUsers"))));
            Assert.That((discord.IsTemporal(), xbox.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(discord.FindCheckConstraint("CK_User_Discord_UsernameNotBlank")!.Sql, Is.EqualTo("TRIM(DiscordUsername) <> ''"));
            Assert.That(Property(discord, nameof(DiscordUser.DiscordUserID)).ValueGenerated, Is.EqualTo(ValueGenerated.Never), "Discord's own ID");
            Assert.That(Property(xbox, nameof(XboxUser.XboxUserID)).ValueGenerated, Is.EqualTo(ValueGenerated.Never), "The XUID");
            Assert.That(IsIgnored(xbox, nameof(XboxUser.LastRenamedAt)), Is.True);
            var byDiscord = ForeignKeyOf(discord, nameof(DiscordUser.User));
            var byXbox = ForeignKeyOf(xbox, nameof(XboxUser.User));
            Assert.That((byDiscord.DeclaringEntityType.ClrType, Names(byDiscord.Properties), byDiscord.IsUnique, byDiscord.IsRequired),
                Is.EqualTo((typeof(User), new List<string> { nameof(User.DiscordUserID) }, true, false)), "A user may have one Discord account");
            Assert.That((byXbox.DeclaringEntityType.ClrType, Names(byXbox.Properties), byXbox.IsUnique, byXbox.IsRequired),
                Is.EqualTo((typeof(User), new List<string> { nameof(User.XboxUserID) }, true, false)), "A user may have one Xbox account");
        }
    }

    [Test]
    public void DuplicateDismissalAndUserExperienceConfigurations_CompositeKeys()
    {
        var dismissal = Configured(new DuplicateDismissalConfiguration());
        var experience = Configured(new UserExperienceConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(dismissal), TableOf(experience)), Is.EqualTo((("Auth", "DuplicateDismissals"), ("Other", "UserExperiences"))));
            Assert.That((dismissal.IsTemporal(), experience.IsTemporal()), Is.EqualTo((false, true)));
            Assert.That(dismissal.FindCheckConstraint("CK_Auth_DuplicateDismissals_Order")!.Sql, Is.EqualTo("LowUserID < HighUserID"),
                "A pair is stored one way round");
            Assert.That(KeyOf(dismissal), Is.EqualTo(new List<string> { nameof(DuplicateDismissal.LowUserID), nameof(DuplicateDismissal.HighUserID) }));
            Assert.That(KeyOf(experience), Is.EqualTo(new List<string> { nameof(UserExperience.UserID), nameof(UserExperience.GameVersionID) }));
        }
    }
}
